using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace GPUAnimator.Player
{
    public class GPUAnimatorPlayer : MonoBehaviour
    {
        [SerializeField]
        BakedTextureAnimation[] animations;

        MeshRenderer meshRenderer;

        TextureAnimations textureAnimations;
        Animator animator;

        MaterialPropertyBlock block;

        private bool isPause;

        BakedTextureAnimation prevAnim;
        BakedTextureAnimation currAnim;

        BakedTextureAnimation prevNextAnim;
        BakedTextureAnimation nextAnim;

        void Start()
        {
            animator = this.GetComponent<Animator>();
            animator.speed = 1;
            animator.enabled = true;
            isPause = false;

            meshRenderer = this.GetComponent<MeshRenderer>();
            textureAnimations = new TextureAnimations(animations);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                animator.speed = 0;
                animator.enabled = false;
                isPause = true;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                animator.speed = 1;
                animator.enabled = true;
                isPause = false;
            }
            if (isPause) { return; }


            if (block == null)
                block = new MaterialPropertyBlock();


            var currAnimState = animator.GetCurrentAnimatorStateInfo(0);

            if (prevAnim == null ||
                textureAnimations.GetShortNameHash(prevAnim.animationName) != currAnimState.shortNameHash)
            {
                currAnim = textureAnimations.Find(currAnimState.shortNameHash);
            }
            else
            {
                currAnim = prevAnim;
            }

            if (currAnim == null)
                return;

            prevAnim = currAnim;


            var nextAnimState = animator.GetNextAnimatorStateInfo(0);

            if (prevNextAnim == null ||
                textureAnimations.GetShortNameHash(prevNextAnim.animationName) != nextAnimState.shortNameHash )
            {
                nextAnim = textureAnimations.Find(nextAnimState.shortNameHash);
            }
            else
            {
                nextAnim = prevNextAnim;
            }

            prevNextAnim = nextAnim;

            //Profiler.BeginSample("MY TASK: COMPUTE");

            if (nextAnim != null && nextAnimState.normalizedTime > 0)
            {
                var transition = animator.GetAnimatorTransitionInfo(0);
                //Debug.Log($"currrPos ={currAnim.positionAnimTexture.name}, nextPos = {nextAnim.positionAnimTexture.name},  transitionNormTime: {transition.normalizedTime}, nextAnim normalizedTime = {nextAnimState.normalizedTime}  ");

                var currAnimNormalizedTime = currAnimState.loop ? currAnimState.normalizedTime % 1 : currAnimState.normalizedTime;
                BakedTextureExtraInfo currAnimInfo = textureAnimations.GetBakedTextureExtraInfo(currAnim.animationName);
                block.SetVector("_TexelSize", currAnimInfo.texelSize);
                block.SetFloat("_NormalizedAnimTime", currAnimNormalizedTime);
                block.SetTexture("PositionAnimTexture", currAnim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture", currAnim.normalAnimTexture);

                var nextAnimNormalizedTime = nextAnimState.loop ? nextAnimState.normalizedTime % 1 : nextAnimState.normalizedTime;
                BakedTextureExtraInfo nextAnimInfo = textureAnimations.GetBakedTextureExtraInfo(currAnim.animationName);
                block.SetVector("_TexelSize_Next", nextAnimInfo.texelSize);
                block.SetFloat("_NormalizedAnimTime_Next", nextAnimNormalizedTime);
                block.SetTexture("PositionAnimTexture_Next", nextAnim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture_Next", nextAnim.normalAnimTexture);

                block.SetFloat("_TransitionTime", transition.normalizedTime);
            }
            else
            {
                var currAnimNormalizedTime = currAnimState.loop ? currAnimState.normalizedTime % 1 : currAnimState.normalizedTime;
                BakedTextureExtraInfo currAnimInfo = textureAnimations.GetBakedTextureExtraInfo(currAnim.animationName);
                block.SetVector("_TexelSize", currAnimInfo.texelSize);
                block.SetFloat("_NormalizedAnimTime", currAnimNormalizedTime);
                block.SetTexture("PositionAnimTexture", currAnim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture", currAnim.normalAnimTexture);

                block.SetVector("_TexelSize_Next", currAnimInfo.texelSize);
                block.SetFloat("_NormalizedAnimTime_Next", currAnimNormalizedTime);
                block.SetTexture("PositionAnimTexture_Next", currAnim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture_Next", currAnim.normalAnimTexture);

                block.SetFloat("_TransitionTime", 0);
            }
            meshRenderer.SetPropertyBlock(block);

            //Profiler.EndSample();
        }
        public void SetBakedTexAnimations(List<BakedTextureAnimation> bakedTextureAnimations)
        {
            this.animations = bakedTextureAnimations.ToArray();
        }
    }
}
