using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using System;

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

        string baseDirPath;
        string objectDirPath;

        // Use this for initialization
        void Start() { }

        public void PlayBake(string objectName)
        {
            anim = GetComponent<Animation>();
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            vCount = skin.sharedMesh.vertexCount;
            texWidth = Mathf.NextPowerOfTwo(vCount);
            mesh = new Mesh();
            bakedTextureAnimations = new List<BakedTextureAnimation>();

            CreateBaseDirectory(objectName);
            CreateObject(objectName);

            foreach (AnimationState state in anim)
            {
                Bake(state);
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

        private void Bake(AnimationState state)
        {
            //yield return new WaitForEndOfFrame();

            Debug.Log("name" + state.name);

            anim.Play(state.name);
            frames = Mathf.NextPowerOfTwo((int)(state.length / 0.05f));
            var dt = state.length / frames;
            time = 0f;
            var infoList = new List<VertInfo>();

            Debug.Log("dt : " + dt);

            pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.{1}.posTex", name, state.name);
            nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.{1}.normTex", name, state.name);
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            Debug.Log("frames : " + frames);

            for (var i = 0; i < frames; i++)
            {
                RecordAnimation(i, state, dt, infoList);
            }

            CreateAssets(state, infoList);
        }

        private void CreateAssets(AnimationState state, List<VertInfo> infoList)
        {
            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("CSMain");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);

            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);

            buffer.Release();

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


        private void RecordAnimation(int index, AnimationState state, float dt, List<VertInfo> infoList)
        {
            state.time = time;
            anim.Sample();
            skin.BakeMesh(mesh);

            infoList.AddRange(Enumerable.Range(0, vCount)
                .Select(idx => new VertInfo()
                {
                    position = mesh.vertices[idx],
                    normal = mesh.normals[idx]
                })
            );

            time += dt;
        }
    }
}
