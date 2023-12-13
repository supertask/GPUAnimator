using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;



using GPUAnimator.Player;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif


namespace GPUAnimator.Baker
{
    public class AnimationTextureBaker : MonoBehaviour
    {
        readonly string folderName = "BakedAnimationTex";


        public ComputeShader infoTexGen;
        public Shader playShader;
        public struct VertInfo
        {
            public Vector3 position;
            public Vector3 normal;
        }

        float time;
        SkinnedMeshRenderer skin;
        int vCount;
        Animation anim;
        int texWidth;
        Mesh mesh;

        int frames;

        RenderTexture pRt;
        RenderTexture nRt;

        List<BakedTextureAnimation> bakedTextureAnimations;

        GameObject go;
        //private GraphicsBuffer meshBuffer;

        string baseDirPath;
        string objectDirPath;

        // Use this for initialization
        void Start()
        {
            //this.PlayBake(this.gameObject.name);
        }
        public async void PlayBake(string objectName)
        {
            anim = GetComponent<Animation>();
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            skin.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            vCount = skin.sharedMesh.vertexCount;
            texWidth = Mathf.NextPowerOfTwo(vCount);
            mesh = new Mesh();
            mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

            bakedTextureAnimations = new List<BakedTextureAnimation>();

            CreateBaseDirectory(objectName);
            CreateObject(objectName);

            foreach (AnimationState state in anim)
            {
                anim.Play(state.name);
                await UniTask.Delay(TimeSpan.FromSeconds(3));
            }

            foreach (AnimationState state in anim)
            {
                await Bake(state);
            }
        }

        private void CreateBaseDirectory(string objectName)
        {
            baseDirPath = Path.Combine("Assets", folderName);

            if (!AssetDatabase.IsValidFolder(baseDirPath))
                AssetDatabase.CreateFolder("Assets", folderName);

            objectDirPath = Path.Combine(baseDirPath, objectName);
            if (!AssetDatabase.IsValidFolder(objectDirPath))
            {
                AssetDatabase.CreateFolder(baseDirPath, objectName);
            }
        }

        GameObject prefab;
        void CreateObject(string objectName)
        {

            Debug.Log("baseDirPath: " + baseDirPath);

            var mat = new Material(playShader);
            mat.SetTexture("_MainTex", skin.sharedMaterial.mainTexture);

            AssetDatabase.CreateAsset(mat, Path.Combine(objectDirPath, string.Format("{0}.mat.asset", objectName)));

            go = new GameObject(objectName);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = skin.sharedMesh;
            go.AddComponent<Animator>();
            go.AddComponent<GPUAnimatorPlayer>();

            go.AddComponent<TextureAnimations>();

            var prefabPath = Path.Combine(objectDirPath, go.name + ".prefab").Replace("\\", "/");

            prefab = PrefabUtility.CreatePrefab(prefabPath, go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private async UniTask Bake(AnimationState state)
        {
            //Debug.Log("name" + state.name);
            anim.Play(state.name);
            
            await UniTask.Delay(TimeSpan.FromSeconds(7));
            
            frames = Mathf.NextPowerOfTwo((int)(state.length / 0.05f));
            var dt = state.length / frames;
            var infoList = new List<VertInfo>();

            //meshBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            //meshBuffer.SetData(infoList.ToArray());

            Debug.Log("dt : " + dt);

            //pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBFloat);
            pRt.name = string.Format("{0}.{1}.posTex", name, state.name);
            //nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBFloat);
            nRt.name = string.Format("{0}.{1}.normTex", name, state.name);
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }
            Debug.Log("frames : " + frames);

            var originalMesh = skin.sharedMesh;

            //this.time = 0f;
            //for (var i = 0; i < frames; i++)
            //{
            //    var success = TestAnimation(i, state, dt, originalMesh);
            //    if (success) {
            //        break;
            //    }
            //}

            this.time = 0f;
            for (var i = 0; i < frames; i++)
            {
                RecordAnimation(i, state, dt, originalMesh);
            }

            CreateAssets(state);


            //meshBuffer?.Release();
        }


        private bool TestAnimation(int index, AnimationState state, float dt, Mesh originalMesh)
        {
            state.time = time;
            anim.Sample();
            GraphicsBuffer positionBuffer = skin.GetVertexBuffer();

            if (positionBuffer != null)
            {
                return true; //success
            }

            //何故か起動直後の何フレームかはVertexBufferが取れない
            Debug.Log($"No VertexBuffer. index = {index}");
            this.time += dt;

            return false;
        }


        //ここのbakeが重い
        private void RecordAnimation(int index, AnimationState state, float dt, Mesh originalMesh)
        {
            state.time = time;
            anim.Sample();
            GraphicsBuffer positionBuffer = skin.GetVertexBuffer();

            //var kernel = infoTexGen.FindKernel("CalcMesh");
            //uint x, y, z;
            //infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            //infoTexGen.SetInt("RecordedFrameIndex", index);
            //infoTexGen.SetInt("VertCount", vCount);
            //infoTexGen.SetBuffer(kernel, "IndexBuffer", originalMesh.GetIndexBuffer());
            //infoTexGen.SetBuffer(kernel, "PositionBuffer", skin.GetVertexBuffer());
            //infoTexGen.SetBuffer(kernel, "Info", meshBuffer);
            //infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);

            if (positionBuffer == null)
            {
                return;
            }


            var kernel = infoTexGen.FindKernel("BakeAnimationTexture");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            infoTexGen.SetInt("RecordedFrameIndex", index);
            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetMatrix("LocalToWorld", skin.worldToLocalMatrix * skin.rootBone.localToWorldMatrix);
            //infoTexGen.SetBuffer(kernel, "Info", meshBuffer);
            infoTexGen.SetBuffer(kernel, "PositionBuffer", positionBuffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, 1, 1);

            positionBuffer?.Release();

            /*
            skin.BakeMesh(mesh);

            infoList.AddRange(Enumerable.Range(0, vCount)
                .Select(idx => new VertInfo()
                {
                    position = mesh.vertices[idx],
                    normal = mesh.normals[idx]
                })
            );
            */

            time += dt;
        }

        private void CreateAssets(AnimationState state)
        {

#if UNITY_EDITOR

            var posTex = RenderTextureToTexture2D.Convert(pRt);
            var normTex = RenderTextureToTexture2D.Convert(nRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);

            var bta = new BakedTextureAnimation();
            bta.fullPathHash = Animator.StringToHash(string.Format("Base Layer.{0}", state.name));
            bta.animationName = state.name;
            bta.positionAnimTexture = posTex;
            bta.normalAnimTexture = normTex;
            bta.texelSize = new Vector4(1.0f / posTex.width, 1.0f / posTex.height, posTex.width, posTex.height);
            bakedTextureAnimations.Add(bta);
            go.GetComponent<TextureAnimations>().SetItemSource(bakedTextureAnimations);

            string posTexPath = Path.Combine(objectDirPath, pRt.name + ".asset");
            string normTexPath = Path.Combine(objectDirPath, nRt.name + ".asset");
            Debug.Log("posTexPath : " + posTexPath + "normTexPath: " + normTexPath);
            AssetDatabase.CreateAsset(posTex, posTexPath);
            AssetDatabase.CreateAsset(normTex, normTexPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            prefab = PrefabUtility.ReplacePrefab(go, prefab);
#endif
        }
    }
}
