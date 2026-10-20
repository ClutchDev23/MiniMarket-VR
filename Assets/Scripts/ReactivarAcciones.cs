using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

[RequireComponent(typeof(InputActionManager))]
public class ReactivarAcciones : MonoBehaviour
{
    InputActionManager m_Manager;

    void Awake()
    {
        m_Manager = GetComponent<InputActionManager>();
    }

    IEnumerator Start()
    {
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            m_Manager.EnableInput();
        }
    }
}
