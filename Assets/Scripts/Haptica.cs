using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Vibracion de los mandos.
///
///   Haptica.Pulso(interactor, 0.3f, 0.05f)  -> vibra el mando que hizo la accion
///   Haptica.Ambos(0.3f, 0.1f)               -> vibran los dos mandos
///
/// Con las manos (sin mandos) no hace nada: no hay motor que vibre.
/// </summary>
public static class Haptica
{
    /// <summary>Vibra el mando de ese interactor (el que agarro, el que aprieta el gatillo...).</summary>
    public static void Pulso(IXRInteractor interactor, float amplitud, float duracion)
    {
        if (interactor is XRBaseInputInteractor mando)
        {
            mando.SendHapticImpulse(amplitud, duracion);
            return;
        }
        Ambos(amplitud, duracion);
    }

    /// <summary>Vibran todos los mandos conectados.</summary>
    public static void Ambos(float amplitud, float duracion)
    {
        foreach (var dev in InputSystem.devices)
            if (dev is XRControllerWithRumble rumble)
                rumble.SendImpulse(Mathf.Clamp01(amplitud), duracion);
    }
}
