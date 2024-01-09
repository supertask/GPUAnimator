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
        Animator animator;
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
            //anim = GetComponent<Animation>();
            animator = GetComponent<Animator>();
            var clips = animator.runtimeAnimatorController.animationClips;

            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            vCount = skin.sharedMesh.vertexCount;
            texWidth = Mathf.NextPowerOfTwo(vCount);
            mesh = new Mesh();
            bakedTextureAnimations = new List<BakedTextureAnimation>();

            CreateBaseDirectory(objectName);
            CreateObject(objectName);

            foreach (AnimationClip clip in clips)
            {
                Bake(clip);
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
            //go.AddComponent<TextureAnimations>();

            var prefabPath = Path.Combine(objectDirPath, go.name + ".prefab").Replace("\\", "/");

            prefab = PrefabUtility.CreatePrefab(prefabPath, go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void Bake(AnimationClip clip)
        {
            Debug.Log("name" + clip.name);
            //animator.Play(clip.name);
            //anim.Play(clip.name);
            frames = Mathf.NextPowerOfTwo((int)(clip.length / 0.05f));
            var dt = clip.length / frames;
            time = 0f;
            var infoList = new List<VertInfo>();

            Debug.Log("dt : " + dt);

            pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = string.Format("{0}.{1}.posTex", name, clip.name);
            nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = string.Format("{0}.{1}.normTex", name, clip.name);
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
                RecordAnimation(i, clip, dt, infoList);
            }

            CreateAssets(clip, infoList);
        }

        private void CreateAssets(AnimationClip clip, List<VertInfo> infoList)
        {
            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("BakeAnimTexture");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            //uint x = 8,  y = 8;

            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetMatrix("TransformMatrix", skin.transform.localToWorldMatrix);
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
            //bta.fullPathHash = Animator.StringToHash(string.Format("Base Layer.{0}", clip.name));
            bta.animationName = clip.name;
            bta.positionAnimTexture = posTex;
            bta.normalAnimTexture = normTex;
            //bta.texelSize = new Vector4(1.0f / posTex.width, 1.0f / posTex.height, posTex.width, posTex.height);
            bakedTextureAnimations.Add(bta);
            go.GetComponent<GPUAnimatorPlayer>().SetBakedTexAnimations(bakedTextureAnimations);

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


        private void RecordAnimation(int index, AnimationClip clip, float dt, List<VertInfo> infoList)
        {
            animator.Play(clip.name, 0, (float)index / frames);
            animator.Update(0);

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
