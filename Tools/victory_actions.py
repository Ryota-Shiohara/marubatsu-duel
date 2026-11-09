"""Export five victory takes per existing rig without editing the source blends.

Blender 4.1: blender --background --factory-startup --python Tools/victory_actions.py
"""
import math
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Marubatsu/Resources/MarubatsuVictory"
CHARACTERS = ("Yagapon", "Mitanuki", "Yaguchi")


def add_action(rig, name, poses, spread, front, side, accent_bones):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data_create().action = action
    for frame, (arms, asymmetry, lean, nod, tuck, accent, crouch) in poses:
        motions = {
            "Body": (side, lean),
            "Head": (side, nod),
            "Arm_L": (front, arms * spread[0] + asymmetry),
            "Arm_R": (front, arms * spread[1] + asymmetry),
            "Leg_L": (side, tuck),
            "Leg_R": (side, tuck * .72),
        }
        for index, bone in enumerate(accent_bones):
            motions[bone] = ((0, 0, 1), accent * (.75 ** index))
        for bone in rig.pose.bones:
            axis, degrees = motions.get(bone.name, ((0, 0, 1), 0))
            to_local = bone.bone.matrix_local.to_3x3().inverted()
            bone.rotation_mode = "QUATERNION"
            bone.rotation_quaternion = Quaternion(to_local @ Vector(axis), math.radians(degrees))
            bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=bone.name)
            if bone.name == "Root":
                bone.location = to_local @ Vector((0, 0, crouch))
                bone.keyframe_insert(data_path="location", frame=frame, group=bone.name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.handle_left_type = key.handle_right_type = "AUTO_CLAMPED"


def export_character(name):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / f"ArtSource/{name}/{name}_Rigged.blend"))
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    bpy.context.scene.render.fps = 30
    # Export only these actions and the original FBX transform hierarchy.
    rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    yagapon = name == "Yagapon"
    mitanuki = name == "Mitanuki"
    spread = (-1, .55) if yagapon else (1, -1) if mitanuki else (-1, 1)
    front = (1, 0, 0) if yagapon else (0, 1, 0)
    side = (0, 1, 0) if yagapon else (-1, 0, 0)
    accents = ("Arrow",) if yagapon else ("Tail_01", "Tail_02", "Tail_03") if mitanuki else ()
    rest = (0, 0, 0, 0, 0, 0, 0)
    launch = (36, -10, -10, 8, -14, 13, 0) if yagapon else (48, 8, -12, 8, -20, 16, 0)
    tucked = (14, -8, 12, -12, 25, -18, 0) if mitanuki else (27, 16, 12, -8, 18, -20, 0)
    punch = (58, 28, -9, 9, -12, 30, 0) if not (yagapon or mitanuki) else (48, -10, -8, 12, -6, 26, 0)
    pose = (46, 4, -5, 8, -8, 8, 0) if yagapon else (49, 0, -6, 9, -10, 12, 0)

    actions = {
        "VictoryTakeoff": [
            (1, rest), (7, (-14, 0, 14, -10, 17, -16, -.10)),
            (12, (15, -8, -14, 12, -20, 21, .025)), (24, launch),
        ],
        "VictorySpin": [
            (1, launch), (8, tucked), (17, tucked), (25, punch), (31, punch),
        ],
        "VictoryPose": [
            (1, punch), (7, (54, -6, -10, 11, -7, 17, .015)), (15, pose),
        ],
        "VictoryLoop": [
            (1, pose), (13, (52, 8, -8, 11, 8, 17, .015)),
            (26, (37, -10, 4, 2, -12, -9, -.01)), (43, pose),
        ],
        "Defeat": [
            (1, rest), (9, (16, 0, -6, 8, -6, 6, .03)),
            (19, (-13, 2, 15, -20, 9, -11, -.045)),
            (37, (-11, 0, 12, -16, 6, -8, -.04)),
        ],
    }
    for kind, poses in actions.items():
        add_action(rig, kind, poses, spread, front, side, accents)
    rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()

    bpy.ops.object.select_all(action="DESELECT")
    for obj in (rig, rig.parent):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(OUTPUT / f"{name}.fbx"), use_selection=True,
        object_types={"ARMATURE", "EMPTY"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False, add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0,
    )
    print("VICTORY_ACTIONS_READY", name, ", ".join(actions))


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for character in CHARACTERS:
        export_character(character)
