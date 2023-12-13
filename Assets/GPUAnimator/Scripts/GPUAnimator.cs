using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

public class GPUAnimator : MonoBehaviour {

    public ComputeBuffer positionBuffer;
    public ComputeBuffer normalBuffer;

    Vector3[] positions;
    Vector3[] normals;

    [SerializeField]
    ComputeShader kernelShader;

    Renderer _renderer;
    MeshFilter mf;

    int vertexCount;

    float normalizedAnimTime;

    TextureAnimations textureAnimations;
    Animator animator;

    int updateAnimation_kernelIndex;
    int transitionAnimation_kernelIndex;

    MaterialPropertyBlock block;


    BakedTextureAnimation prev_anim;
    BakedTextureAnimation curr_anim;

    BakedTextureAnimation prev_next_anim;
    BakedTextureAnimation next_anim;
    // Use this for initialization
    void Start ()
    {
        animator = this.GetComponent<Animator>();
        _renderer = this.GetComponent<Renderer>();
        mf = this.GetComponent<MeshFilter>();
        textureAnimations = this.GetComponent<TextureAnimations>();

        vertexCount = mf.mesh.vertexCount;

        InitKernelIndex();

        InitBuffer();
    }

    private void InitKernelIndex()
    {
        updateAnimation_kernelIndex = kernelShader.FindKernel("UpdateAnimation");
        transitionAnimation_kernelIndex = kernelShader.FindKernel("TransitionAnimation");
    }

    private void InitBuffer()
    {
        positionBuffer = new ComputeBuffer(vertexCount, Marshal.SizeOf(typeof(Vector3)));
        positions = new Vector3[vertexCount];
        positionBuffer.SetData(positions);

        normalBuffer = new ComputeBuffer(vertexCount, Marshal.SizeOf(typeof(Vector3)));
        normals = new Vector3[vertexCount];
        normalBuffer.SetData(normals);
    }

    void Update () {

        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            if(Random.value > 0.5f)
                animator.SetBool("OnRun", true);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            animator.SetBool("OnRun", false);
        }


        Profiler.BeginSample("MY TASK: Animation");
            if (block == null)
                block = new MaterialPropertyBlock();

            var currAnimState = animator.GetCurrentAnimatorStateInfo(0);

            if (prev_anim == null || prev_anim.fullPathHash != currAnimState.fullPathHash)
            {
                curr_anim = textureAnimations.Find(currAnimState.fullPathHash);
            }
            else
            {
                curr_anim = prev_anim;
            }

            if (curr_anim == null)
                return;

            prev_anim = curr_anim;

            kernelShader.SetVector("_TexelSize", curr_anim.texelSize);
            kernelShader.SetFloat("_NormalizedAnimTime", currAnimState.normalizedTime % 1.0f);


            var nextAnimState = animator.GetNextAnimatorStateInfo(0);

            if (prev_next_anim == null || prev_next_anim.fullPathHash != nextAnimState.fullPathHash)
            {
                next_anim = textureAnimations.Find(nextAnimState.fullPathHash);
            }
            else
            {
                next_anim = prev_next_anim;
            }

            prev_next_anim = next_anim;
        Profiler.EndSample();


        //ここが重たい
        //Profiler.BeginSample("MY TASK: COMPUTE");

        if (next_anim != null && nextAnimState.normalizedTime > 0)
        {

            var transition = animator.GetAnimatorTransitionInfo(0);
            //Debug.Log($"currAnim = {curr_anim.animationName}, currrPos ={curr_anim.positionAnimTexture.name},  nextAnimName = {next_anim.animationName} nextPos = {next_anim.positionAnimTexture.name},  transitionNormTime: {transition.normalizedTime} ");
            Debug.Log($"currrPos ={curr_anim.positionAnimTexture.name}, nextPos = {next_anim.positionAnimTexture.name},  transitionNormTime: {transition.normalizedTime}, nextAnim normalizedTime = {nextAnimState.normalizedTime}  ");

            kernelShader.SetTexture(transitionAnimation_kernelIndex, "PositionAnimTexture", curr_anim.positionAnimTexture);
            kernelShader.SetTexture(transitionAnimation_kernelIndex, "NormalAnimTexture", curr_anim.normalAnimTexture);

            kernelShader.SetTexture(transitionAnimation_kernelIndex, "PositionAnimTexture_Next", next_anim.positionAnimTexture);
            kernelShader.SetTexture(transitionAnimation_kernelIndex, "NormalAnimTexture_Next", next_anim.normalAnimTexture);
            kernelShader.SetVector("_TexelSize_Next", next_anim.texelSize);

            kernelShader.SetFloat("_NormalizedAnimTime_Next", nextAnimState.normalizedTime % 1.0f);

            kernelShader.SetFloat("_TransitionTime", transition.normalizedTime);
            

            kernelShader.SetBuffer(transitionAnimation_kernelIndex, "PositionBuffer", positionBuffer);
            kernelShader.SetBuffer(transitionAnimation_kernelIndex, "NormalBuffer", normalBuffer);

            kernelShader.Dispatch(transitionAnimation_kernelIndex, vertexCount / 1024 + 1, 1, 1);
        }
        else
        {
            Debug.Log($"No next anim. currAnim: {curr_anim.positionAnimTexture.name}");
            kernelShader.SetTexture(updateAnimation_kernelIndex, "PositionAnimTexture", curr_anim.positionAnimTexture);
            kernelShader.SetTexture(updateAnimation_kernelIndex, "NormalAnimTexture", curr_anim.normalAnimTexture);

            kernelShader.SetBuffer(updateAnimation_kernelIndex, "PositionBuffer", positionBuffer);
            kernelShader.SetBuffer(updateAnimation_kernelIndex, "NormalBuffer", normalBuffer);

            kernelShader.Dispatch(updateAnimation_kernelIndex, vertexCount / 1024 + 1, 1, 1);
        }
        //Profiler.EndSample();

        //Profiler.BeginSample("MY TASK: Material block");
        block.SetBuffer("PositionBuffer", positionBuffer);
        block.SetBuffer("NormalBuffer", normalBuffer);

        _renderer.SetPropertyBlock(block);
        //Profiler.EndSample();

    }

    private void OnDisable()
    {
        if (positionBuffer != null)
            positionBuffer.Release();

        if (normalBuffer != null)
            normalBuffer.Release();
    }
}
