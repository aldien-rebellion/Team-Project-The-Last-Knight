using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;

public class ShowAllSkills : MonoBehaviour
{
    private Animator animator;
    private string[] states = {
        "Volcanox_Walk",
        "Volcanox_Infernal_Slam",
        "Volcanox_Magma_Punch",
        "Volcanox_Dragon_Breath"
    };
    private int currentIndex = 0;
    private float timer = 0f;
    private float switchInterval = 4f; // แสดงแต่ละท่า 4 วินาที

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.Play(states[0]);
        }
    }

    void Update()
    {
        if (animator == null) return;
        timer += Time.deltaTime;
        if (timer >= switchInterval)
        {
            timer = 0f;
            currentIndex = (currentIndex + 1) % states.Length;
            animator.Play(states[currentIndex]);
            Debug.Log("Playing: " + states[currentIndex]);
        }
    }
}
