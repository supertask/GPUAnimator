using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace GPUAnimator.Player
{
    public class GPUAnimatorPlayer : MonoBehaviour
    {

        MeshRenderer meshRenderer;

        TextureAnimations textureAnimations;
        Animator animator;

        MaterialPropertyBlock block;

        private bool isPause;

        BakedTextureAnimation prev_anim;
        BakedTextureAnimation curr_anim;

        BakedTextureAnimation prev_next_anim;
        BakedTextureAnimation next_anim;

        void Start()
        {
            animator = this.GetComponent<Animator>();
            animator.speed = 1;
            animator.enabled = true;
            isPause = false;

            meshRenderer = this.GetComponent<MeshRenderer>();
            textureAnimations = this.GetComponent<TextureAnimations>();
            textureAnimations.Init();
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

            if (prev_anim == null ||
                textureAnimations.GetShortNameHash(prev_anim.animationName) != currAnimState.shortNameHash)
            {
                curr_anim = textureAnimations.Find(currAnimState.shortNameHash);
            }
            else
            {
                curr_anim = prev_anim;
            }

            if (curr_anim == null)
                return;

            prev_anim = curr_anim;


            var nextAnimState = animator.GetNextAnimatorStateInfo(0);

            if (prev_next_anim == null ||
                textureAnimations.GetShortNameHash(prev_next_anim.animationName) != nextAnimState.shortNameHash )
            {
                next_anim = textureAnimations.Find(nextAnimState.shortNameHash);
            }
            else
            {
                next_anim = prev_next_anim;
            }

            prev_next_anim = next_anim;

            //Profiler.BeginSample("MY TASK: COMPUTE");

            if (next_anim != null && nextAnimState.normalizedTime > 0)
            {
                var transition = animator.GetAnimatorTransitionInfo(0);
                //Debug.Log($"currrPos ={curr_anim.positionAnimTexture.name}, nextPos = {next_anim.positionAnimTexture.name},  transitionNormTime: {transition.normalizedTime}, nextAnim normalizedTime = {nextAnimState.normalizedTime}  ");

                block.SetVector("_TexelSize", curr_anim.texelSize);
                block.SetFloat("_NormalizedAnimTime", currAnimState.normalizedTime % 1.0f);
                block.SetTexture("PositionAnimTexture", curr_anim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture", curr_anim.normalAnimTexture);

                block.SetVector("_TexelSize_Next", next_anim.texelSize);
                block.SetFloat("_NormalizedAnimTime_Next", nextAnimState.normalizedTime % 1.0f);
                block.SetTexture("PositionAnimTexture_Next", next_anim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture_Next", next_anim.normalAnimTexture);

                block.SetFloat("_TransitionTime", transition.normalizedTime);
            }
            else
            {

                block.SetVector("_TexelSize", curr_anim.texelSize);
                block.SetFloat("_NormalizedAnimTime", currAnimState.normalizedTime % 1.0f);
                block.SetTexture("PositionAnimTexture", curr_anim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture", curr_anim.normalAnimTexture);

                block.SetVector("_TexelSize_Next", curr_anim.texelSize);
                block.SetFloat("_NormalizedAnimTime_Next", currAnimState.normalizedTime % 1.0f);
                block.SetTexture("PositionAnimTexture_Next", curr_anim.positionAnimTexture);
                block.SetTexture("NormalAnimTexture_Next", curr_anim.normalAnimTexture);

                block.SetFloat("_TransitionTime", 0);
            }
            meshRenderer.SetPropertyBlock(block);

            //Profiler.EndSample();
        }

        private void OnDisable()
        {
        }
    }
}
