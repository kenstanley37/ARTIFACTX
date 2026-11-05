using System.Numerics;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData.Services;

/// <summary>Shared scene-local transform composition used by station and planetary asset inspection.</summary>
internal static class SceneTransformMath
{
    internal static Matrix4x4 Local(TkTransformData transform)
    {
        const float radians = MathF.PI / 180f;
        var matrix = Matrix4x4.CreateScale(transform.ScaleX, transform.ScaleY, transform.ScaleZ)
            * Matrix4x4.CreateRotationX(transform.RotX * radians)
            * Matrix4x4.CreateRotationY(transform.RotY * radians)
            * Matrix4x4.CreateRotationZ(transform.RotZ * radians);
        matrix.Translation = new Vector3(transform.TransX, transform.TransY, transform.TransZ);
        return matrix;
    }
}
