using Neo.ConsolePlus;
using UnityEngine;

public sealed class DebugCommands : MonoBehaviour
{
    private GameObject spawnedCube;

    [NeoCommand("debug.spawn_cube", "Spawns a cube in front of the main camera.")]
    private void SpawnCube()
    {
        Camera cam = Camera.main;
        Vector3 position = cam != null ? cam.transform.position + cam.transform.forward * 5f : Vector3.zero;
        spawnedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spawnedCube.name = "Neo Debug Cube";
        spawnedCube.transform.position = position;
    }

    [NeoCommand("debug.move_self", "Moves this command component GameObject. Tests instance targets.")]
    private void MoveSelf(float x, float y, float z)
    {
        transform.position = new Vector3(x, y, z);
    }

    [NeoCommandRuntimeOnly("debug.runtime_message", "Runs only in the Runtime Console.")]
    private static string RuntimeMessage()
    {
        return "Runtime-only command executed.";
    }

    [NeoCommandEditorOnly("debug.editor_message", "Runs only in the Editor Console.")]
    private static string EditorMessage()
    {
        return "Editor-only command executed.";
    }
}
