using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HorsePlayer : MonoBehaviour
{
    Animator animator;

    private void Start()
    {
        animator = this.GetComponent<Animator>();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Alpha0))
        {
            animator.SetBool("OnIdle", true);
            animator.SetBool("OnWalk", false);
            animator.SetBool("OnRun", false);
        }

        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            animator.SetBool("OnIdle", false);
            animator.SetBool("OnWalk", true);
            animator.SetBool("OnRun", false);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            animator.SetBool("OnIdle", false);
            animator.SetBool("OnWalk", false);
            animator.SetBool("OnRun", true);
        }        
    }
}
