using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(InstaRack.InstaRackMod), "InstaRack", "1.0.2", "derrick")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace InstaRack
{
	public sealed class InstaRackMod : MelonMod
	{
		public static PlayerManager playerManager => PlayerManager.instance;

		public override void OnInitializeMelon()
		{
			HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
		}

		private static bool IsPlacingRack()
		{
			return playerManager != null && playerManager.objectInHand == PlayerManager.ObjectInHand.Rack;
		}

		private static UsableObject GetHeldRack()
		{
			if (playerManager?.objectInHandGO == null)
			{
				return null;
			}

			for (int i = 0; i < playerManager.objectInHandGO.Count; i++)
			{
				GameObject heldObject = playerManager.objectInHandGO[i];
				if (heldObject == null)
				{
					continue;
				}

				UsableObject usableObject = heldObject.GetComponent<UsableObject>();
				if (usableObject != null)
				{
					return usableObject;
				}

				usableObject = heldObject.GetComponentInChildren<UsableObject>(true);
				if (usableObject != null)
				{
					return usableObject;
				}
			}

			return null;
		}

		private static bool TryGetRackColor(UsableObject heldRack, out Color color)
		{
			color = default;
			if (heldRack?.saveValue == null || heldRack.saveValue.Length < 7)
			{
				return false;
			}

			color = new Color(
				heldRack.saveValue[3],
				heldRack.saveValue[4],
				heldRack.saveValue[5],
				heldRack.saveValue[6]);
			return true;
		}

		private static void CopyRackState(RackMount rackMount, UsableObject heldRack)
		{
			if (rackMount == null || heldRack == null)
			{
				return;
			}

			rackMount.saveValue = heldRack.saveValue;
			rackMount.saveIntArray = heldRack.saveIntArray;
			rackMount.saveIntArray2 = heldRack.saveIntArray2;
		}

		private static void ApplyRackColor(GameObject rack, Color rackColor)
		{
			if (rack == null)
			{
				return;
			}

			foreach (Renderer renderer in rack.GetComponentsInChildren<Renderer>(true))
			{
				if (renderer == null)
				{
					continue;
				}

				Material[] materials = renderer.materials;
				bool changedRenderer = false;

				for (int i = 0; i < materials.Length; i++)
				{
					Material material = materials[i];
					if (material == null || !material.name.Contains("BrushedAluminiumRack", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (material.HasProperty("_Color"))
					{
						material.color = rackColor;
					}

					if (material.HasProperty("_BaseColor"))
					{
						material.SetColor("_BaseColor", rackColor);
					}

					changedRenderer = true;
				}

				if (changedRenderer)
				{
					renderer.materials = materials;
				}
			}
		}

		private static bool PlaceRack(RackMount rackMount)
		{
			if (rackMount == null || rackMount.isRackInstantiated)
			{
				return false;
			}

			UsableObject heldRack = GetHeldRack();
			CopyRackState(rackMount, heldRack);

			InteractObjectData saveData = new(rackMount);
			GameObject rack = rackMount.InstantiateRack(saveData);
			if (rack == null)
			{
				return false;
			}

			if (TryGetRackColor(heldRack, out Color rackColor))
			{
				ApplyRackColor(rack, rackColor);
			}

			rackMount.isRackInstantiated = true;

			if (playerManager.objectInHandGO != null)
			{
				for (int i = 0; i < playerManager.objectInHandGO.Count; i++)
				{
					GameObject heldObject = playerManager.objectInHandGO[i];
					if (heldObject == null)
					{
						continue;
					}

					UnityEngine.Object.Destroy(heldObject);
					playerManager.objectInHandGO[i] = null;
				}
			}

			playerManager.numberOfObjectsInHand = 0;
			playerManager.objectInHand = PlayerManager.ObjectInHand.None;

			playerManager.enabledMouseMovement = true;
			playerManager.enabledPlayerMovement = true;
			playerManager.enabledRayLookInteract = true;
			
			playerManager.LockedCursorForPlayerMovement();

			Image waitImage = playerManager.imageWaitForAction;
			if (waitImage != null)
			{
				waitImage.fillAmount = 0f;
				waitImage.enabled = false;
			}

			return true;
		}

		[HarmonyPatch(typeof(RackMount), nameof(RackMount.InteractOnClick))]
		private static class RackMountInteractPatch
		{
			private static bool Prefix(RackMount __instance)
			{
				if (!IsPlacingRack())
				{
					return true;
				}

				try
				{
					if (!PlaceRack(__instance))
					{
						return true;
					}

					return false;
				}
				catch
				{
					return true;
				}
			}
		}
	}
}
