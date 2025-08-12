using UnityEngine;

public class Attack : StateMachineBehaviour
{
    private Vector3 speed;
    private NPCScript nPC;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        nPC = animator.GetComponent<NPCScript>();
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false && nPC.isServer) {
            nPC.navMeshAgent.isStopped = true;
            speed = nPC.navMeshAgent.velocity;
            nPC.navMeshAgent.velocity = Vector3.zero;
            nPC.RotateTowardPlayer();
        }

    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.GetBool("die") == false && nPC.isServer) {
            nPC.hasAttacked = false;
            nPC.navMeshAgent.isStopped = false;
            nPC.navMeshAgent.velocity = speed;
        }
    }

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
