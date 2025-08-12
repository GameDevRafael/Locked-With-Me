using Mirror.Examples.BenchmarkIdle;
using UnityEngine;

public class Investigate : StateMachineBehaviour
{
    private NPCScript nPC;


    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false) {
            nPC = animator.GetComponent<NPCScript>();

            if (nPC.isServer) {
                nPC.navMeshAgent.SetDestination(nPC.soundTriggerHeard.position);
                nPC.navMeshAgent.speed = 6.5f;
            }

        }
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false && nPC.isServer) {
            nPC.navMeshAgent.SetDestination(nPC.soundTriggerHeard.position);

            float distanceToSound = Vector3.Distance(nPC.transform.position, nPC.soundTriggerHeard.position);
            if (distanceToSound <= 2f) {
                nPC.heardNoise = false;
            }
        }
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    //override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    
    //}

    // OnStateMove is called right after Animator.OnAnimatorMove()
    //override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that processes and affects root motion
    //}

    // OnStateIK is called right after Animator.OnAnimatorIK()
    //override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    //{
    //    // Implement code that sets up animation IK (inverse kinematics)
    //}
}
