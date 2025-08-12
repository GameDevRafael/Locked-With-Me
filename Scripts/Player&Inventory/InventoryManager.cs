using System.Collections.Generic;
using NUnit.Framework.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour {
    private List<GameObject> inventorySlots = new();

    void Start() {
        Transform canvas = GameObject.Find("Canvas").transform;

        for (int i = 1; i <= 6; i++) {
            string slotName = $"QuickSlot ({i})";
            Transform slot = canvas.Find("GameInterface").Find("Inventory").Find(slotName);
            inventorySlots.Add(slot.gameObject);
        }
    }

    public GameObject AddItem(string itemName) {
        ItemData itemData = Resources.Load<ItemData>($"Blueprints/{itemName}");
        GameObject slot;

        if (itemData.isStackable) {
            slot = FindStackableSlot(itemData);

            if(slot == null) {
                slot = FindEmptySlot(itemData);
            }

            if(slot != null) {
                slot.transform.Find("Item").Find("CounterBackground").Find("Counter").GetComponent<TextMeshProUGUI>().text = "x" + slot.transform.Find("Item").GetComponent<InventoryItem>().quantity.ToString();
                slot.transform.Find("Item").Find("CounterBackground").gameObject.SetActive(true);
            }


        } else {
            slot = FindEmptySlot(itemData);

            if(slot != null)
                slot.transform.Find("Item").Find("CounterBackground").gameObject.SetActive(false);
        }

        return slot;
    }

    private GameObject FindStackableSlot(ItemData itemData) {
        foreach(GameObject slot in inventorySlots) {
            Transform item = slot.transform.Find("Item");
            if (item.gameObject.activeSelf == false) continue; // se não estiver ativo é porque não sería um slot em que podemos juntar o item que queremos então seguimos

            // metemos true porque os gameObjects poderão estar inativos e assim iria retornar null
            InventoryItem inventoryItem = item.GetComponentInChildren<InventoryItem>(true);

            // como todos os itens são de quantidade 1, não precisamos de encher até ficar cheio e procurar por outro slot para o resto
            if (inventoryItem.itemName == itemData.name && inventoryItem.quantity + itemData.quantity <= itemData.maxStackSize) {
                inventoryItem.quantity += itemData.quantity;
                return slot;
            }
        }

        return null;
    }

    private GameObject FindEmptySlot(ItemData itemData) {
        foreach (GameObject slot in inventorySlots) {
            Transform item = slot.transform.Find("Item");
            if (!item.gameObject.activeSelf) {
                InventoryItem inventoryItem = item.GetComponent<InventoryItem>();
                item.gameObject.GetComponent<Image>().sprite = itemData.icon;

                inventoryItem.itemName = itemData.name;
                inventoryItem.quantity = itemData.quantity;

                item.gameObject.SetActive(true);
                return slot;
            }
        }

        return null;
    }

    public bool HasItem(string itemName) {
        foreach (GameObject slot in inventorySlots) {
            Transform item = slot.transform.Find("Item");
            if (item.gameObject.activeSelf) {
                InventoryItem inventoryItem = item.GetComponent<InventoryItem>();
                if(inventoryItem.itemName == itemName)  
                    return true;
            }
        }

        return false;
    }

    public InventoryItem FindItem(string itemName) {
        ItemData itemData = FindBlueprint(itemName);
        return FindItem(itemData);
    }


    public bool HasKey(string lockName) {
        InventoryItem item = null;

        switch (lockName) {
            case "blackLock":
                item = FindItem(FindBlueprint("blackKey"));
                break;

            case "greenLock":
                item = FindItem(FindBlueprint("greenKey"));
                break;

            case "goldenLock":
                item = FindItem(FindBlueprint("goldenKey"));
                break;

            case "blueLock":
                item = FindItem(FindBlueprint("blueKey"));
                break;

            case "redLock":
                item = FindItem(FindBlueprint("redKey"));
                break;
        }

        return item != null;
    }

    public GameObject FindKey(string lockName) {
        InventoryItem item = null;

        switch (lockName) {
            case "blackLock":
                item = FindItem(FindBlueprint("blackKey"));
                break;

            case "greenLock":
                item = FindItem(FindBlueprint("greenKey"));
                break;

            case "goldenLock":
                item = FindItem(FindBlueprint("goldenKey"));
                break;

            case "blueLock":
                item = FindItem(FindBlueprint("blueKey"));
                break;

            case "redLock":
                item = FindItem(FindBlueprint("redKey"));
                break;
        }

        return item.gameObject;
    }

    private InventoryItem FindItem(ItemData itemData) {
        foreach (GameObject slot in inventorySlots) {
            Transform item = slot.transform.Find("Item");
            if (item.gameObject.activeSelf) {
                InventoryItem inventoryItem = item.GetComponent<InventoryItem>();
                if (inventoryItem.itemName == itemData.name) {
                    return inventoryItem;
                }
            }
        }

        return null;
    }

    private ItemData FindBlueprint(string itemName) {
        return Resources.Load<ItemData>($"Blueprints/{itemName}");
    }


    public void RemoveItem(GameObject itemToRemove) {
        foreach (GameObject slot in inventorySlots) {
            Transform item = slot.transform.Find("Item");
            if (item.gameObject == itemToRemove) {
                item.gameObject.SetActive(false);
                return;
            }
        }
    }

    public void RemoveItem(string itemName, int value) {
        InventoryItem item = FindItem(itemName);

        item.quantity -= value;
        item.transform.Find("CounterBackground").Find("Counter").GetComponent<TextMeshProUGUI>().text = "x" + item.quantity;


        if (item.quantity == 0) {
            item.gameObject.SetActive(false);
        }
    }
}