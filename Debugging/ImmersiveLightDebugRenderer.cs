using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ImmersiveLight.Debugging
{
    internal sealed class ImmersiveLightDebugRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly Matrixf modelView = new();
        private MeshRef meshRef;
        private BlockPos origin;
        private int meshVersion = -1;

        internal ImmersiveLightDebugRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public double RenderOrder => 1.95;
        public int RenderRange => 9999;

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!ImmersiveLightDebug.RaysEnabled)
            {
                if (meshRef != null)
                {
                    RebuildMesh();
                }

                return;
            }

            if (meshVersion != ImmersiveLightDebug.Version)
            {
                RebuildMesh();
            }

            if (meshRef == null)
            {
                return;
            }

            IShaderProgram prog = capi.Shader.GetProgram((int)EnumShaderProgram.Autocamera);
            prog.Use();

            capi.Render.LineWidth = ImmersiveLightDebug.LineWidth;
            capi.Render.BindTexture2d(0);
            capi.Render.GLDisableDepthTest();

            Vec3d cameraPos = capi.World.Player.Entity.CameraPos;
            modelView.Set(capi.Render.CameraMatrixOrigin);
            modelView.Translate((float)(origin.X - cameraPos.X), (float)(origin.Y - cameraPos.Y), (float)(origin.Z - cameraPos.Z));

            prog.UniformMatrix("projectionMatrix", capi.Render.CurrentProjectionMatrix);
            prog.UniformMatrix("modelViewMatrix", modelView.Values);

            capi.Render.RenderMesh(meshRef);

            prog.Stop();
            capi.Render.GLEnableDepthTest();
        }

        private void RebuildMesh()
        {
            List<LightDebugLine> lines = ImmersiveLightDebug.Snapshot(out meshVersion);
            meshRef?.Dispose();
            meshRef = null;

            if (lines.Count == 0)
            {
                return;
            }

            LightDebugLine first = lines[0];
            origin = new BlockPos((int)Math.Floor(first.From.X), (int)Math.Floor(first.From.Y), (int)Math.Floor(first.From.Z));

            MeshData mesh = new(lines.Count * 2, lines.Count * 2, false, false, true, true)
            {
                mode = EnumDrawMode.Lines
            };

            int vertexIndex = 0;
            foreach (LightDebugLine line in lines)
            {
                mesh.AddVertexSkipTex((float)(line.From.X - origin.X), (float)(line.From.Y - origin.Y), (float)(line.From.Z - origin.Z), line.Color);
                mesh.AddIndex(vertexIndex++);
                mesh.AddVertexSkipTex((float)(line.To.X - origin.X), (float)(line.To.Y - origin.Y), (float)(line.To.Z - origin.Z), line.Color);
                mesh.AddIndex(vertexIndex++);
            }

            meshRef = capi.Render.UploadMesh(mesh);
        }

        public void Dispose()
        {
            meshRef?.Dispose();
            meshRef = null;
        }
    }
}
