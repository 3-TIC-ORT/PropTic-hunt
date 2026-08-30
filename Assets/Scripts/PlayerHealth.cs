using Photon.Pun;
using UnityEngine;

/// <summary>
/// Vive en la raíz del Prefab "Player".
/// Recibe el RPC de daño y maneja la muerte, sincronizada
/// para ambos clientes (el RPC se ejecuta en todos a la vez).
/// </summary>
public class PlayerHealth : MonoBehaviourPun
{
    public int maxHealth = 1;
    private int currentHealth;
    public bool IsDead { get; private set; }

    void Start()
    {
        currentHealth = maxHealth;
        IsDead = false;
    }

    [PunRPC]
    public void RPC_TakeDamage()
    {
        if (IsDead) return;

        currentHealth--;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        IsDead = true;
        Debug.Log(gameObject.name + " fue eliminado.");

        // Muerte simple por ahora: se desactiva para ambos clientes,
        // ya que este método corre a partir del mismo RPC en todos.
        gameObject.SetActive(false);
    }
}
