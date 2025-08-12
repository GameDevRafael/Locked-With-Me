using UnityEngine;

public class Walk : StateMachineBehaviour
{
    private NPCScript nPC;
    private float distanceToWP;






    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false) {
            nPC = animator.GetComponent<NPCScript>();

            if (nPC.isServer) {
                nPC.MoveToNextWayPoint();
                nPC.navMeshAgent.speed = 2.0f;
            }

        }
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false && nPC.isServer) {
            distanceToWP = (nPC.transform.position - nPC.wayPointsRandom[nPC.currentTarget].position).magnitude;
            nPC.networkDistanceToWP = distanceToWP;

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
