using UnityEngine;

namespace HackSlash.Core
{
    public class GroundCheck : MonoBehaviour
    {
        [SerializeField] private Transform probe;
        [SerializeField] private float radius = 0.15f;
        [SerializeField] private LayerMask groundMask;

        public bool IsGrounded { get; private set; }

        private void FixedUpdate()
        {
            if (probe == null) return;
            IsGrounded = Physics2D.OverlapCircle(probe.position, radius, groundMask);
        }

        private void OnDrawGizmosSelected()
        {
            if (probe == null) return;
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(probe.position, radius);
        }
    }
}
