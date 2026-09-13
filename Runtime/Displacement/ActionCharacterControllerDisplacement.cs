using UnityEngine;

namespace Ethan.ActionEditor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActionPlayer), typeof(CharacterController))]
    public sealed class ActionCharacterControllerDisplacement : MonoBehaviour
    {
        [SerializeField] MonoBehaviour rootMotionCollector;
        [SerializeField] Animator animator;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            GetComponent<ActionPlayer>().ConfigureDisplacement(
                new CharacterControllerBackend(GetComponent<CharacterController>()),
                rootMotion: rootMotionCollector as IActionRootMotionSource,
                animator: animator);
        }
    }
}
