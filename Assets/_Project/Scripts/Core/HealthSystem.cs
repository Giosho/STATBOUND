using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private int maxHealth = 5;

    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool IsDead => Current <= 0;

    /// <summary>Arguments: current, max.</summary>
    public event Action<int, int> Changed;
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0) return;

        Current = Mathf.Max(0, Current - amount);
        Changed?.Invoke(Current, maxHealth);

        if (Current == 0)
            Died?.Invoke();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }
}
