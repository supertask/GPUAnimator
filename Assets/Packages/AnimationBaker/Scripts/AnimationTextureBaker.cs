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

        RenderTexture pRt;
        RenderTexture nRt;

        List<BakedTextureAnimation> bakedTextureAnimations;

        GameObject go;
        //private GraphicsBuffer meshBuffer;

        string baseDirPath;
        string objectDirPath;
        private Animator animator;
        private AnimationClip[] animatorClips;
        private int currentClipIndex = 0;

        private void Awake()
        {
            Application.targetFrameRate = 30;
        }

        void Start()
        {
            StartCoroutine(PlaySequentially());
        }
        public void Init()
        {
            animator = GetComponent<Animator>();
            animatorClips = animator.runtimeAnimatorController.animationClips;

            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            skin.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            vCount = skin.sharedMesh.vertexCount;
            texWidth = Mathf.NextPowerOfTwo(vCount);
            //mesh = new Mesh();
            //mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

            bakedTextureAnimations = new List<BakedTextureAnimation>();

            string objectName = this.gameObject.name;
            CreateBaseDirectory(objectName);
            CreateObject(objectName);
        }

        IEnumerator PlaySequentially()
        {
            yield return new WaitForSeconds(3.0f);

            this.Init();
            while(currentClipIndex < animatorClips.Length)
            {
                var clip = animatorClips[currentClipIndex];
                Prepare(clip);
                animator.speed = 0;
                animator.enabled = true;
                animator.Play(clip.name);
                yield return StartCoroutine(WaitForAnimation(animator, 0));
                CreateAssets(clip);
                currentClipIndex++;
            }
        }

        IEnumerator WaitForAnimation(Animator animator, int layerIndex)
        {
            bool isAnimationPlaying = true;
            while (isAnimationPlaying)
            {
                //フレームごとに実行される
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
                int interpolatedFrameIndex = (int)(stateInfo.normalizedTime * this.texHeight);
                RecordAnimation(interpolatedFrameIndex);
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    RecordAnimation(this.texHeight - 1);
                    isAnimationPlaying = false;
                }
                yield return null;
            }
        }

        private int texHeight = 0;

        private void Prepare(AnimationClip clip)
        {
            int numOfFrames = (int)(clip.length / 0.05f);
            this.texHeight = Mathf.NextPowerOfTwo(numOfFrames);
            //var dt = clip.length / texHeight;

            Debug.Log($"clip length = {clip.length}, texHeight = {texHeight}");

            //pRt = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBHalf);
            pRt = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBFloat);
            pRt.name = string.Format("{0}.{1}.posTex", name, clip.name);

            //nRt = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBHalf);
            nRt = new RenderTexture(texWidth, texHeight, 0, RenderTextureFormat.ARGBFloat);
            nRt.name = string.Format("{0}.{1}.normTex", name, clip.name);
            foreach (var rt in new[] { pRt, nRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }
        }


        private void RecordAnimation(int interpolatedFrameIndex)
        {
            GraphicsBuffer positionBuffer = skin.GetVertexBuffer();

            if (positionBuffer == null)
            {
                //何故か起動直後の何フレームかはVertexBufferが取れない
                Debug.Log($"No VertexBuffer. index = {interpolatedFrameIndex}");
                return;
            }

            var kernel = infoTexGen.FindKernel("BakeAnimationTexture");
            uint x, y, z;
            infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
            infoTexGen.SetInt("RecordedFrameIndex", interpolatedFrameIndex);
            infoTexGen.SetInt("VertCount", vCount);
            //infoTexGen.SetMatrix("LocalToWorld", skin.worldToLocalMatrix * skin.rootBone.localToWorldMatrix);

            infoTexGen.SetVector("_TexSize", new Vector2(texWidth, texHeight));
            infoTexGen.SetMatrix("RootBoneLocalToWorld", skin.rootBone.localToWorldMatrix);
            infoTexGen.SetMatrix("TransformLocalToWorld", skin.transform.localToWorldMatrix);
            infoTexGen.SetBuffer(kernel, "PositionBuffer", positionBuffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, 1, 1);

            positionBuffer?.Release();
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



        private void CreateAssets(AnimationClip clip)
        {

#if UNITY_EDITOR

            var posTex = RenderTextureToTexture2D.Convert(pRt);
            var normTex = RenderTextureToTexture2D.Convert(nRt);
            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);

            var bta = new BakedTextureAnimation();
            //bta.fullPathHash = Animator.StringToHash(string.Format("Base Layer.{0}", state.name));
            //bta.animationName = state.name;
            bta.fullPathHash = Animator.StringToHash(string.Format("Base Layer.{0}", clip.name));
            bta.animationName = clip.name;
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
