using UnityEngine;
using UnityEngine.UI;

public class Toolbar : MonoBehaviour
{
	[SerializeField] private World world;
	[SerializeField] private Player player;

	[SerializeField] private RectTransform highlight;
	[SerializeField] private ItemSlot[] ItemSlots;

	private int slotIndex = 0;

	private void Start()
	{
		player.SelectedBlockIndex = ItemSlots[slotIndex].ItemID;

		foreach (ItemSlot slot in ItemSlots)
		{
			slot.Icon.sprite = world.BlockTypes[slot.ItemID].Icon;
			slot.Icon.enabled = true;
		}
	}

	private void Update()
	{
		float _scroll = Input.GetAxis("Mouse ScrollWheel");

		if(_scroll != 0)
		{
			if(_scroll > 0)
			{
				slotIndex--;
			}
			else
			{
				slotIndex++;
			}

			if(slotIndex > ItemSlots.Length - 1)
			{
				slotIndex = 0;
			}
			else if(slotIndex < 0)
			{
				slotIndex = ItemSlots.Length - 1;
			}

			highlight.position = ItemSlots[slotIndex].Icon.transform.position;
			player.SelectedBlockIndex = ItemSlots[slotIndex].ItemID;
		}
	}
}

[System.Serializable]
public class ItemSlot
{
	public byte ItemID;
	public Image Icon;
}
