using System;
using UnityEngine;

/// <summary>
/// Needs a SpriteRenderer, one solid Collider2D and one trigger Collider2D on the same object.
/// Sealed = no exit here, Locked = exit waiting for a correct answer, Open = passable.
/// </summary>
[DisallowMultipleComponent]
public class DoorController : MonoBehaviour
{
    public enum DoorState
    {
        Sealed,
        Locked,
        Open
    }

    [SerializeField] private DoorDirection direction;
    [SerializeField] private Color sealedColor = new Color32(0x5A, 0x4E, 0x6E, 0xFF);
    [SerializeField] private Color lockedColor = new Color32(0x9A, 0x2F, 0x2F, 0xFF);
    [SerializeField] private Color openColor = new Color32(0x07, 0x05, 0x0C, 0xFF);

    private SpriteRenderer sr;
    private Collider2D solid;
    private Collider2D passage;

    public DoorDirection Direction => direction;
    public DoorState State { get; private set; } = DoorState.Sealed;

    public event Action<DoorController> PlayerEntered;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        foreach (var c in GetComponents<Collider2D>())
        {
            if (c.isTrigger) passage = c;
            else solid = c;
        }

        if (sr == null || solid == null || passage == null)
            Debug.LogError("DoorController on '" + name + "' needs a SpriteRenderer, a solid collider and a trigger collider.", this);

        SetState(State);
    }

    public void SetState(DoorState state)
    {
        State = state;

        if (sr != null)
            sr.color = state == DoorState.Open ? openColor : state == DoorState.Locked ? lockedColor : sealedColor;
        if (solid != null) solid.enabled = state != DoorState.Open;
        if (passage != null) passage.enabled = state == DoorState.Open;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (State != DoorState.Open) return;
        if (other.GetComponent<PlayerController>() == null) return;

        PlayerEntered?.Invoke(this);
    }
}
