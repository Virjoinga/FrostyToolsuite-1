using Frosty.Core.Extensions;
using Frosty.Core.Screens;
using Frosty.Core.Viewport;
using FrostySdk;
using FrostySdk.Managers.Entries;
using MeshSetPlugin.Render;
using MeshSetPlugin.Resources;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using DXUT = Frosty.Core.Viewport.DXUT;

namespace MeshSetPlugin.Screens
{
    public class MeshAndPreviewContainer
    {
        public MeshSet Mesh;
        public MeshRenderMesh Preview;
        public MeshMaterialCollection Materials;
        public bool bUpdateMaterials;
        public Matrix4x4 Transform;
        public int MeshId;

        public MeshAndPreviewContainer(int inMeshId, MeshSet inMesh, MeshRenderMesh inPreview, Matrix4x4 inTransform, MeshMaterialCollection inMaterials)
        {
            MeshId = inMeshId;
            Mesh = inMesh;
            Preview = inPreview;
            Materials = inMaterials;
            Transform = inTransform;
        }
    }

    public delegate void RenderAction(RenderCreateState state);

    public class MultiMeshPreviewScreen : DeferredRenderScreen2
    {
        public int CurrentLOD { get; set; }
        public bool IsLoading { get; set; }
        public bool ShowSkeleton { get; set; } = false;
        public MeshRenderSkeleton VisualizeSkeleton { get; set; }

        private SkeletonRenderShape skeletonJointSphere;
        private SkeletonRenderShape skeletonBoneShape;

        private List<MeshAndPreviewContainer> renderMeshes = new List<MeshAndPreviewContainer>();
        private List<LightRenderInstance> renderLights = new List<LightRenderInstance>();

        private int currentMeshId = 0;
        private int currentLightId = 0;
        private Queue<RenderAction> renderTasks = new Queue<RenderAction>();

        // @temp
        private MeshRenderAnim anim;

        public MultiMeshPreviewScreen()
        {
        }

        public MeshSet GetMesh(int meshId)
        {
            if (meshId >= renderMeshes.Count)
            {
                return null;
            }

            return renderMeshes[meshId].Mesh;
        }

        public int AddMesh(MeshSet mesh, MeshMaterialCollection materials, Matrix4x4 transform, MeshRenderSkeleton skeleton = null)
        {
            int meshId = currentMeshId;
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                // just build a dummy one if one wasnt provided
                if (skeleton == null)
                {
                    skeleton = new MeshRenderSkeleton();
                }

                IsLoading = true;
                MeshRenderMesh renderMesh = new MeshRenderMesh(state, mesh, materials, skeleton);
                renderMeshes.Add(new MeshAndPreviewContainer(meshId, mesh, renderMesh, transform, materials));
                IsLoading = false;
            });

            return currentMeshId++;
        }

        public int AddLight(LightRenderType type, Matrix4x4 transform, Vector3 color, float intensity, float attenuationRadius, float sphereRadius)
        {
            renderLights.Add(new LightRenderInstance()
            {
                Type = type,
                Transform = transform,
                Color = color,
                Intensity = intensity,
                AttenuationRadius = attenuationRadius,
                SphereRadius = sphereRadius,
                LightId = currentLightId
            });
            return currentLightId++;
        }

        public void ModifyLight(int lightId, Matrix4x4 transform, Vector3 color, float intensity, float attenuationRadius, float sphereRadius)
        {
            int idx = renderLights.FindIndex((LightRenderInstance inst) => inst.LightId == lightId);
            if (idx != -1)
            {
                renderLights[idx] = new LightRenderInstance()
                {
                    Type = renderLights[lightId].Type,
                    Transform = transform,
                    Color = color,
                    Intensity = intensity,
                    AttenuationRadius = attenuationRadius,
                    SphereRadius = sphereRadius,
                    LightId = renderLights[idx].LightId
                };
            }
        }

        public void RemoveMesh(int meshId)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                MeshAndPreviewContainer meshContainer = renderMeshes.Find((MeshAndPreviewContainer a) => a.MeshId == meshId);
                if (meshContainer != null)
                {
                    meshContainer.Preview.Dispose();
                    renderMeshes.Remove(meshContainer);
                }
            });
        }

        public void RemoveLight(int lightId)
        {
            int idx = renderLights.FindIndex((LightRenderInstance inst) => inst.LightId == lightId);
            if (idx != -1)
            {
                renderLights.RemoveAt(idx);
            }
        }

        public void ClearMeshes(bool clearAll = false)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                if (!clearAll && renderMeshes.Count > 0)
                {
                    MeshAndPreviewContainer rootMesh = renderMeshes[0];
                    renderMeshes.Clear();
                    renderMeshes.Add(rootMesh);
                }
                else
                {
                    renderMeshes.Clear();
                }
            });

            if (clearAll)
            {
                currentMeshId = 0;
            }
        }

        public void ClearLights()
        {
            renderLights.Clear();
        }

        public void LoadMaterials(int meshId, MeshMaterialCollection inMaterials)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                MeshAndPreviewContainer meshContainer = renderMeshes.Find((MeshAndPreviewContainer a) => a.MeshId == meshId);
                meshContainer.Preview.SetMaterials(RenderCreateState, inMaterials);
            });
        }

        public void SetTransform(int meshId, Matrix4x4 transform)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                MeshAndPreviewContainer meshContainer = renderMeshes.Find((MeshAndPreviewContainer a) => a.MeshId == meshId);
                meshContainer.Transform = transform;
            });
        }

        public void SetMeshSectionSelected(int meshId, int lodId, int sectionId, bool newSelected)
        {
            MeshAndPreviewContainer meshContainer = renderMeshes.Find((MeshAndPreviewContainer a) => a.MeshId == meshId);
            meshContainer.Preview.GetLod(lodId).GetSection(sectionId).IsSelected = newSelected;
        }

        public void SetMeshSectionVisible(int meshId, int lodId, int sectionId, bool newVisible)
        {
            MeshAndPreviewContainer meshContainer = renderMeshes.Find((MeshAndPreviewContainer a) => a.MeshId == meshId);
            meshContainer.Preview.GetLod(lodId).GetSection(sectionId).IsVisible = newVisible;
        }

        public void SetDistantLightProbeTexture(EbxAssetEntry entry)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                DistantLightProbe = (entry != null)
                    ? state.TextureLibrary.LoadTextureAsset(entry.Guid, true)
                    : null;
            });
        }

        public void SetLookupTableTexture(EbxAssetEntry entry)
        {
            renderTasks.Enqueue((RenderCreateState state) =>
            {
                LookupTable = (entry != null)
                    ? state.TextureLibrary.LoadTextureAsset(entry.Guid)
                    : null;
            });
        }

        public void SetAnimation(MeshRenderAnim inAnim)
        {
            anim = inAnim;
        }

        public void SetTexture(string name, string paramName, EbxAssetEntry texture)
        {
            //if (!meshes.ContainsKey(name))
            //    return;

            //MeshSet mesh = meshes[name].Mesh;
            //for (int i = 0; i < mesh.Lods[0].Sections.Count; i++)
            //    SetTexture(name, i, paramName, texture);
        }

        public void SetTexture(string name, int sectionId, string paramName, EbxAssetEntry texture, int uvChannel = 0)
        {
            //if (!meshes.ContainsKey(name))
            //    return;

            //MeshRenderMesh preview = meshes[name].Preview;
            //if (preview == null)
            //{
            //    MeshAndPreviewContainer mesh = meshes[name];
            //    mesh.PreviewLoaded += (o, e) => { SetTexture(name, sectionId, paramName, texture, uvChannel); };
            //    return;
            //}

            //MeshRenderLod lod = preview.GetLod(0);
            //if (lod.GetFallbackSection(sectionId) == null)
            //    return;

            //MeshSetPreviewSection section = lod.GetFallbackSection(sectionId);
            //ShaderResourceView srv = null;

            //if (paramName == "base")
            //{
            //    srv = section.DiffuseTexture;
            //    section.DiffuseTexture = LoadTextureAsset(texture.Guid);
            //}
            //else if (paramName == "normal")
            //{
            //    srv = section.NormTexture;
            //    section.NormTexture = LoadTextureAsset(texture.Guid);
            //}
            //else if (paramName == "coeff")
            //{
            //    srv = section.MaskTexture;
            //    section.MaskTexture = LoadTextureAsset(texture.Guid);
            //}

            //if (uvChannel != 0)
            //    section.CustomParam3 = 1;

            //if (srv != null)
            //    UnloadTexture(srv);
        }

        public override void CreateBuffers()
        {
            if (camera == null)
            {
                camera = new DXUT.FirstPersonCamera();
                camera.SetViewParams(new Vector3(0, 1.5f, 4.0f), new Vector3(0, 0, 0));
            }

            base.CreateBuffers();

            skeletonJointSphere = SkeletonRenderShape.CreateSphere(RenderCreateState, "SkeletonJoint", 1.0f, 8, new Vector4(0.1f, 0.6f, 0.6f, 1.0f));
            skeletonBoneShape = SkeletonRenderShape.CreateCube(RenderCreateState, "SkeletonBone", 1.0f, 1.0f, 1.0f, new Vector4(0.3f, 0.8f, 0.8f, 1.0f));
        }

        public override void DisposeBuffers()
        {
            foreach (MeshAndPreviewContainer mesh in renderMeshes)
            {
                mesh.Preview?.Dispose();
            }
            renderMeshes.Clear();

            skeletonJointSphere?.Dispose();
            skeletonBoneShape?.Dispose();

            base.DisposeBuffers();
        }

        public override void Closed()
        {
            base.Closed();
        }

        public override void Update(double timestep)
        {
            base.Update(timestep);

            if (anim != null)
            {
                anim.Update(timestep);
                foreach (MeshAndPreviewContainer mesh in renderMeshes)
                {
                    foreach (MeshRenderSection section in mesh.Preview.GetLod(CurrentLOD).Sections)
                    {
                        anim.UpdateSkeleton(section.Skeleton);
                    }
                }

                if (renderMeshes.Count > 0)
                {
                    foreach (MeshRenderSection section in renderMeshes[0].Preview.GetLod(CurrentLOD).Sections)
                    {
                        VisualizeSkeleton = section.Skeleton;
                        break;
                    }
                }
            }
        }

        bool oneTimeAction = true;
        public override void Render()
        {
            while (renderTasks.Count > 0)
            {
                RenderAction action = renderTasks.Dequeue();
                action.Invoke(RenderCreateState);
            }

            // @hack
            if (renderMeshes.Count > 0 && oneTimeAction)
            {
                CenterView();
                oneTimeAction = false;
            }

            base.Render();

            if (!ShowSkeleton || VisualizeSkeleton == null || skeletonJointSphere == null || skeletonBoneShape == null)
                return;

            var jointInstances = new List<MeshRenderInstance>();
            var boneInstances = new List<MeshRenderInstance>();
            int total = VisualizeSkeleton.TotalBoneCount;

            for (int i = 0; i < total; i++)
            {
                Vector3 pos = VisualizeSkeleton.GetBoneWorldMatrix(i).Translation;
                int parentId = VisualizeSkeleton.GetBone(i).ParentBoneId;

                float sphereR = 0.015f;

                if (parentId >= 0)
                {
                    Vector3 parentPos = VisualizeSkeleton.GetBoneWorldMatrix(parentId).Translation;
                    float len = (pos - parentPos).Length();

                    if (len > 0.001f)
                    {
                        sphereR = Math.Max(0.008f, Math.Min(0.025f, len * 0.08f));
                        float boneWidth = sphereR * 0.4f;
                        AddSkeletonBone(boneInstances, parentPos, pos, boneWidth);
                    }
                }

                jointInstances.Add(new MeshRenderInstance
                {
                    RenderMesh = skeletonJointSphere,
                    Transform = Matrix4x4.CreateScale(sphereR) * Matrix4x4.CreateTranslation(pos)
                });
            }

            Viewport.Context.OMSetRenderTargets(Viewport.ColorBufferRTV, null);

            Viewport.Context.OMSetDepthStencilState(D3DUtils.CreateDepthStencilState(false), 0);
            Viewport.Context.OMSetBlendState(D3DUtils.CreateBlendState(D3DUtils.CreateBlendStateRenderTarget()));
            Viewport.Context.VSSetConstantBuffer(0, viewConstants.Buffer);
            Viewport.Context.PSSetConstantBuffer(0, viewConstants.Buffer);
            Viewport.Context.RSSetState(D3DUtils.CreateRasterizerState(CullMode.Back, depthClip: false));

            // Bones first so joints draw on top of them.
            RenderMeshes(MeshRenderPath.Forward, boneInstances);
            RenderMeshes(MeshRenderPath.Forward, jointInstances);
        }

        private void AddSkeletonBone(List<MeshRenderInstance> instances, Vector3 from, Vector3 to, float width)
        {
            Vector3 dir = to - from;
            float len = dir.Length();
            if (len < 0.0001f) return;
            dir = Vector3.Normalize(dir);
            Vector3 mid = (from + to) * 0.5f;
            Vector3 up = Vector3.UnitY;
            Vector3 axis = Vector3.Cross(up, dir);
            float dot = Vector3.Dot(up, dir);
            Quaternion rot = (axis.LengthSquared() > 0.0001f)
                ? Quaternion.Normalize(new Quaternion(axis, 1.0f + dot))
                : (dot < 0 ? new Quaternion(Vector3.UnitX, 0) : Quaternion.Identity);
            instances.Add(new MeshRenderInstance
            {
                RenderMesh = skeletonBoneShape,
                Transform = Matrix4x4.CreateScale(width, len, width)
                           * Matrix4x4.CreateFromQuaternion(rot)
                           * Matrix4x4.CreateTranslation(mid)
            });
        }

        public override List<MeshRenderInstance> CollectMeshInstances()
        {
            List<MeshRenderInstance> instances = new List<MeshRenderInstance>();
            foreach (MeshAndPreviewContainer mesh in renderMeshes)
            {
                MeshRenderLod lod = mesh.Preview.GetLod(CurrentLOD);
                MeshRenderInstance inst = new MeshRenderInstance()
                {
                    RenderMesh = lod,
                    Transform = mesh.Transform
                };
                instances.Add(inst);
            }
            return instances;
        }

        public override List<LightRenderInstance> CollectLightInstances()
        {
            return renderLights;
        }

        public override void CharTyped(char ch)
        {
            base.CharTyped(ch);
            if (ch == 'f')
            {
                CenterView();
            }
        }

        public void CenterView()
        {
            BoundingBox aabb = CalcWorldBoundingBox();
            if (camera is DXUT.ModelViewerCamera mvCamera)
            {
                mvCamera.Reset();
                mvCamera.SetLookAtPt(aabb.Min + (aabb.Max - aabb.Min) * 0.5f);
                mvCamera.SetEyePt(aabb.Min + (aabb.Max - aabb.Min) * 0.5f + Vector3.UnitY);
                mvCamera.SetRadius((aabb.Max - aabb.Min).Length() * 1.0f);
            }
            else if (camera is DXUT.FirstPersonCamera fpCamera)
            {
                Vector3 center = aabb.Min + (aabb.Max - aabb.Min) * 0.5f;
                Vector3 offset = center + new Vector3(0, Math.Abs(aabb.Max.Y - aabb.Min.Y) / 1.25f, (aabb.Max - aabb.Min).Length() * 0.9f);
                if (offset.Y < 0.5f)
                {
                    offset.Y = 0.5f;
                }

                fpCamera.SetViewParams(offset, center);
            }
        }

        protected override BoundingBox CalcWorldBoundingBox()
        {
            BoundingBox aabb = new BoundingBox();
            int i = 0;

            foreach (MeshAndPreviewContainer mesh in renderMeshes)
            {
                BoundingBox bb = mesh.Preview.Bounds;
                bb.Min = (bb.Min + mesh.Transform.Translation) * new Vector3(-1, 1, 1);
                bb.Max = (bb.Max + mesh.Transform.Translation) * new Vector3(-1, 1, 1);

                float tmp = bb.Min.X;
                bb.Min.X = bb.Max.X;
                bb.Max.X = tmp;

                if (i++ == 0)
                {
                    aabb = bb;
                }
                else
                {
                    aabb = BoundingBox.CreateMerged(aabb, bb);
                }
            }

            return aabb;
        }
    }


    // Custom skeleton shape renderer

    public class SkeletonRenderShape : MeshRenderBase, IDisposable
    {
        private struct ShapeVertex
        {
            public Vector3 Pos;
            public Vector3 Normal;
            public Vector2 TexCoord;

            public ShapeVertex(Vector3 p, Vector3 n, Vector2 t)
            {
                Pos = p;
                Normal = n;
                TexCoord = t;
            }
        }

        public override string DebugName => name;

        private ID3D11Buffer vertexBuffer;
        private ID3D11Buffer indexBuffer;
        private ID3D11Buffer pixelParameters;
        private List<ID3D11ShaderResourceView> pixelTextures = new();
        private ShaderPermutation permutation;
        private int indexCount = 0;
        private string name;

        public static SkeletonRenderShape CreateSphere(RenderCreateState state, string inName, float radius, int tessellation, Vector4 color)
        {
            List<ShapeVertex> vertices = new List<ShapeVertex>();
            int verticalSegments = tessellation;
            int horizontalSegments = tessellation * 2;

            for (int i = 0; i <= verticalSegments; i++)
            {
                float v = 1 - (float)i / verticalSegments;
                float latitude = (float)((i * Math.PI / verticalSegments) - (Math.PI / 2.0f));
                float dy = 0.0f, dxz = 0.0f;

                DirectXMathUtils.XMScalarSinCos(ref dy, ref dxz, latitude);

                for (int j = 0; j <= horizontalSegments; j++)
                {
                    float u = (float)j / horizontalSegments;
                    float longitude = (float)(j * (Math.PI * 2) / horizontalSegments);
                    float dx = 0.0f, dz = 0.0f;

                    DirectXMathUtils.XMScalarSinCos(ref dx, ref dz, longitude);

                    dx *= dxz;
                    dz *= dxz;

                    Vector3 normal = new Vector3(dx, dy, dz);
                    Vector3 pos = normal * radius;

                    vertices.Add(new ShapeVertex(pos, Vector3.TransformCoordinate(normal, Matrix4x4.CreateScale(-1, 1, -1)), Vector2.Zero));
                }
            }

            int stride = horizontalSegments + 1;
            List<ushort> indices = new List<ushort>();

            for (int i = 0; i < verticalSegments; i++)
            {
                for (int j = 0; j <= horizontalSegments; j++)
                {
                    int nextI = i + 1;
                    int nextJ = (j + 1) % stride;

                    indices.Add((ushort)(i * stride + nextJ));
                    indices.Add((ushort)(i * stride + j));
                    indices.Add((ushort)(nextI * stride + j));

                    indices.Add((ushort)(nextI * stride + nextJ));
                    indices.Add((ushort)(i * stride + nextJ));
                    indices.Add((ushort)(nextI * stride + j));
                }
            }

            return new SkeletonRenderShape(state, inName, vertices, indices, color);
        }

        public static SkeletonRenderShape CreateCube(RenderCreateState state, string inName, float width, float height, float depth, Vector4 color)
        {
            const int faceCount = 6;
            Vector3[] faceNormals = new Vector3[faceCount]
            {
                new Vector3(0,0,1),
                new Vector3(0,0,-1),
                new Vector3(1,0,0),
                new Vector3(-1,0,0),
                new Vector3(0,1,0),
                new Vector3(0,-1,0),
            };
            Vector2[] texCoords = new Vector2[4]
            {
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1),
                new Vector2(0, 0)
            };

            Vector3 tsize = new Vector3(width, height, depth);
            tsize /= 2;

            List<ShapeVertex> vertices = new List<ShapeVertex>();
            List<ushort> indices = new List<ushort>();

            for (int i = 0; i < faceCount; i++)
            {
                Vector3 normal = faceNormals[i];
                Vector3 basis = (i >= 4) ? Vector3.UnitZ : Vector3.UnitY;
                Vector3 side1 = Vector3.Cross(normal, basis);
                Vector3 side2 = Vector3.Cross(normal, side1);

                int vbase = vertices.Count;
                indices.Add((ushort)(vbase + 2));
                indices.Add((ushort)(vbase + 1));
                indices.Add((ushort)(vbase + 0));

                indices.Add((ushort)(vbase + 3));
                indices.Add((ushort)(vbase + 2));
                indices.Add((ushort)(vbase + 0));

                vertices.Add(new ShapeVertex((normal - side1 - side2) * tsize, normal, texCoords[0]));
                vertices.Add(new ShapeVertex((normal - side1 + side2) * tsize, normal, texCoords[1]));
                vertices.Add(new ShapeVertex((normal + side1 + side2) * tsize, normal, texCoords[2]));
                vertices.Add(new ShapeVertex((normal + side1 - side2) * tsize, normal, texCoords[3]));
            }

            return new SkeletonRenderShape(state, inName, vertices, indices, color);
        }

        private unsafe SkeletonRenderShape(RenderCreateState state, string inName, List<ShapeVertex> vertices, List<ushort> indices, Vector4 color)
        {
            using (Vortice.DataStream stream = new(indices.Count * 2, false, true))
            {
                stream.WriteRange<ushort>(indices.ToArray());
                stream.Position = 0;
                //indexBuffer = new state.Device.CreateBuffer(state.Device, stream, indices.Count * 2, SharpDX.Direct3D11.ResourceUsage.Default, SharpDX.Direct3D11.BindFlags.IndexBuffer, SharpDX.Direct3D11.CpuAccessFlags.None, SharpDX.Direct3D11.ResourceOptionFlags.None, 2);
                ReadOnlySpan<byte> data = new(stream.BaseUnsafePointer, indices.Count * 2);
                indexBuffer = state.Device.CreateBuffer(data, BindFlags.IndexBuffer, structureByteStride: 2);
            }
            using (Vortice.DataStream stream = new(vertices.Count * (4 * 8), false, true))
            {
                stream.WriteRange<ShapeVertex>(vertices.ToArray());
                stream.Position = 0;
                //vertexBuffer = new SharpDX.Direct3D11.Buffer(state.Device, stream, vertices.Count * (4 * 8), SharpDX.Direct3D11.ResourceUsage.Default, SharpDX.Direct3D11.BindFlags.VertexBuffer, SharpDX.Direct3D11.CpuAccessFlags.None, SharpDX.Direct3D11.ResourceOptionFlags.None, (4 * 8));
                ReadOnlySpan<byte> data = new(stream.BaseUnsafePointer, vertices.Count * (4 * 8));
                vertexBuffer = state.Device.CreateBuffer(data, BindFlags.VertexBuffer, structureByteStride: 4 * 8);
            }

            GeometryDeclarationDesc geomDecl = GeometryDeclarationDesc.Create(new GeometryDeclarationDesc.Element[]
            {
                new GeometryDeclarationDesc.Element() { Usage = VertexElementUsage.Pos,       Format = VertexElementFormat.Float3 },
                new GeometryDeclarationDesc.Element() { Usage = VertexElementUsage.Normal,    Format = VertexElementFormat.Float3 },
                new GeometryDeclarationDesc.Element() { Usage = VertexElementUsage.TexCoord0, Format = VertexElementFormat.Float2 },
            });

            permutation = state.ShaderLibrary.GetUserShader("GroundPlane", geomDecl);
            permutation.LoadShaders(state.Device);

            // Override the shader's default (red/green) color parameters with calm teal. (Edit: that didn't work but i like the color)
            List<ShaderParameter> customParams = new List<ShaderParameter>()
            {
                new ShaderParameter("Color1", ShaderParameterType.Float4, color.X, color.Y, color.Z, color.W),
                new ShaderParameter("Color2", ShaderParameterType.Float4, color.X, color.Y, color.Z, color.W),
                new ShaderParameter("SMR",    ShaderParameterType.Float4, 0.5f, 0.0f, 1.0f, 1.0f)
            };
            permutation.AssignParameters(state, customParams, new List<ShaderParameter>(), ref pixelParameters, ref pixelTextures);

            indexCount = indices.Count;
            name = inName;
        }

        public override void Render(ID3D11DeviceContext context, MeshRenderPath renderPath)
        {
            if (renderPath == MeshRenderPath.Shadows || renderPath == MeshRenderPath.Selection)
                return;

            context.IASetIndexBuffer(indexBuffer, Vortice.DXGI.Format.R16_UInt, 0);
            context.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
            context.IASetVertexBuffer(0, vertexBuffer, 4 * 8);

            permutation.SetState(context, renderPath);
            context.PSSetConstantBuffer(2, pixelParameters);
            context.PSSetShaderResources(1, pixelTextures.ToArray());

            context.DrawIndexed((uint)indexCount, 0, 0);
        }

        public void Dispose()
        {
            pixelParameters?.Dispose();
            indexBuffer?.Dispose();
            vertexBuffer?.Dispose();
            pixelTextures.Clear();
        }
    }
}