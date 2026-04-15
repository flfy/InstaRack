using System;
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(InstaRack.InstaRackMod), "InstaRack", "1.0.3", "derrick")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace InstaRack
{
	public sealed class InstaRackMod : MelonMod
	{
		public static PlayerManager playerManager => PlayerManager.instance;

		private static Il2CppStructArray<float> pendingUnmountSaveValue;
		private static Il2CppStructArray<int> pendingUnmountSaveIntArray;
		private static Il2CppStructArray<int> pendingUnmountSaveIntArray2;
		private static bool hasPendingUnmountColor;
		private static Color pendingUnmountColor;
		private static int pendingUnmountTicket;

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

		private static bool TryGetRackColor(Interact source, out Color color)
		{
			color = default;
			if (source?.saveValue == null || source.saveValue.Length < 7)
			{
				return false;
			}

			color = new Color(
				source.saveValue[3],
				source.saveValue[4],
				source.saveValue[5],
				source.saveValue[6]);
			return true;
		}

		private static bool TryGetRackColor(InteractObjectData data, out Color color)
		{
			color = default;
			if (data?.value == null || data.value.Length < 7)
			{
				return false;
			}

			color = new Color(
				data.value[3],
				data.value[4],
				data.value[5],
				data.value[6]);
			return true;
		}

		private static Il2CppStructArray<float> CloneArray(Il2CppStructArray<float> source)
		{
			if (source == null)
			{
				return null;
			}

			Il2CppStructArray<float> clone = new(source.Length);
			for (int i = 0; i < source.Length; i++)
			{
				clone[i] = source[i];
			}

			return clone;
		}

		private static Il2CppStructArray<int> CloneArray(Il2CppStructArray<int> source)
		{
			if (source == null)
			{
				return null;
			}

			Il2CppStructArray<int> clone = new(source.Length);
			for (int i = 0; i < source.Length; i++)
			{
				clone[i] = source[i];
			}

			return clone;
		}

		private static void CopyRackState(Interact target, Interact source)
		{
			if (target == null || source == null)
			{
				return;
			}

			target.saveValue = CloneArray(source.saveValue);
			target.saveIntArray = CloneArray(source.saveIntArray);
			target.saveIntArray2 = CloneArray(source.saveIntArray2);
		}

		private static void SetRackColor(Interact target, Color color)
		{
			if (target == null)
			{
				return;
			}

			Il2CppStructArray<float> value = CloneArray(target.saveValue);
			if (value == null)
			{
				value = new Il2CppStructArray<float>(7);
			}
			else if (value.Length < 7)
			{
				Il2CppStructArray<float> expanded = new(7);
				for (int i = 0; i < value.Length; i++)
				{
					expanded[i] = value[i];
				}

				value = expanded;
			}

			value[3] = color.r;
			value[4] = color.g;
			value[5] = color.b;
			value[6] = color.a;
			target.saveValue = value;
		}

		private static InteractObjectData CreateRackData(RackMount rackMount, UsableObject heldRack)
		{
			InteractObjectData saveData = new(rackMount);
			if (heldRack == null)
			{
				return saveData;
			}

			saveData.value = CloneArray(heldRack.saveValue);
			saveData.saveIntArray = CloneArray(heldRack.saveIntArray);
			saveData.saveIntArray2 = CloneArray(heldRack.saveIntArray2);
			return saveData;
		}

		private static void ApplyColor(GameObject root, Color color, string materialName)
		{
			if (root == null)
			{
				return;
			}

			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
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
					if (material == null || !material.name.Contains(materialName, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (material.HasProperty("_Color"))
					{
						material.color = color;
					}

					if (material.HasProperty("_BaseColor"))
					{
						material.SetColor("_BaseColor", color);
					}

					changedRenderer = true;
				}

				if (changedRenderer)
				{
					renderer.materials = materials;
				}
			}
		}

		private static void ApplyRackColor(GameObject rack, Color rackColor)
		{
			ApplyColor(rack, rackColor, "BrushedAluminiumRack");
		}

		private static void ApplyBoxColor(GameObject boxedRack, Color rackColor)
		{
			ApplyColor(boxedRack, rackColor, "BoxedRack");
		}

		private static void RefreshBoxColor(UsableObject boxedRack)
		{
			if (!IsRackBox(boxedRack) || !TryGetRackColor(boxedRack, out Color rackColor))
			{
				return;
			}

			SetRackColor(boxedRack, rackColor);
			ApplyBoxColor(boxedRack.gameObject, rackColor);
		}

		private static void RefreshBoxColor(UsableObject boxedRack, InteractObjectData data)
		{
			if (!IsRackBox(boxedRack))
			{
				return;
			}

			if (!TryGetRackColor(data, out Color rackColor) && !TryGetRackColor(boxedRack, out rackColor))
			{
				return;
			}

			SetRackColor(boxedRack, rackColor);
			ApplyBoxColor(boxedRack.gameObject, rackColor);
		}

		private static bool IsRackBox(UsableObject usableObject)
		{
			if (usableObject == null)
			{
				return false;
			}

			string name = usableObject.gameObject?.name ?? usableObject.name ?? string.Empty;
			return usableObject.objectInHandType == PlayerManager.ObjectInHand.Rack
				|| name.Contains("BoxedRack", StringComparison.OrdinalIgnoreCase);
		}

		private static bool HasPendingUnmountState()
		{
			return pendingUnmountSaveValue != null
				|| pendingUnmountSaveIntArray != null
				|| pendingUnmountSaveIntArray2 != null
				|| hasPendingUnmountColor;
		}

		private static void ClearPendingUnmountState()
		{
			pendingUnmountSaveValue = null;
			pendingUnmountSaveIntArray = null;
			pendingUnmountSaveIntArray2 = null;
			hasPendingUnmountColor = false;
			pendingUnmountColor = default;
		}

		private static IEnumerator WaitForUnmountBox(int ticket)
		{
			for (int frame = 0; frame < 180; frame++)
			{
				if (ticket != pendingUnmountTicket || !HasPendingUnmountState())
				{
					yield break;
				}

				UsableObject heldRack = GetHeldRack();
				if (IsRackBox(heldRack))
				{
					ApplyPendingUnmountState(heldRack);
					yield break;
				}

				yield return null;
			}
		}

		private static void CaptureUnmountState(Rack rack)
		{
			ClearPendingUnmountState();

			RackMount rackMount = rack?.rackMount;
			if (rackMount == null)
			{
				return;
			}

			pendingUnmountSaveValue = CloneArray(rackMount.saveValue);
			pendingUnmountSaveIntArray = CloneArray(rackMount.saveIntArray);
			pendingUnmountSaveIntArray2 = CloneArray(rackMount.saveIntArray2);
			hasPendingUnmountColor = TryGetRackColor(rackMount, out pendingUnmountColor);
			pendingUnmountTicket++;
			MelonCoroutines.Start(WaitForUnmountBox(pendingUnmountTicket));
		}

		private static void ApplyPendingUnmountState(UsableObject boxedRack)
		{
			if (boxedRack == null || !HasPendingUnmountState())
			{
				return;
			}

			boxedRack.saveValue = CloneArray(pendingUnmountSaveValue);
			boxedRack.saveIntArray = CloneArray(pendingUnmountSaveIntArray);
			boxedRack.saveIntArray2 = CloneArray(pendingUnmountSaveIntArray2);

			if (hasPendingUnmountColor)
			{
				SetRackColor(boxedRack, pendingUnmountColor);
				ApplyBoxColor(boxedRack.gameObject, pendingUnmountColor);
			}

			ClearPendingUnmountState();
		}

		private static bool PlaceRack(RackMount rackMount)
		{
			if (rackMount == null || rackMount.isRackInstantiated)
			{
				return false;
			}

			UsableObject heldRack = GetHeldRack();
			InteractObjectData saveData = CreateRackData(rackMount, heldRack);
			GameObject rack = rackMount.InstantiateRack(saveData);
			if (rack == null)
			{
				return false;
			}

			UsableObject placedRack = rack.GetComponent<UsableObject>() ?? rack.GetComponentInChildren<UsableObject>(true);
			if (placedRack != null && TryGetRackColor(heldRack, out Color placedRackColor))
			{
				SetRackColor(placedRack, placedRackColor);
			}

			if (TryGetRackColor(heldRack, out Color rackColor))
			{
				SetRackColor(rackMount, rackColor);
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
					return !PlaceRack(__instance);
				}
				catch
				{
					return true;
				}
			}
		}

		[HarmonyPatch(typeof(RackMount), nameof(RackMount.InstantiateRack))]
		private static class RackMountInstantiatePatch
		{
			private static void Postfix(RackMount __instance, InteractObjectData saveData, GameObject __result)
			{
				if (__result == null)
				{
					return;
				}

				if (!TryGetRackColor(saveData, out Color rackColor) && !TryGetRackColor(__instance, out rackColor))
				{
					return;
				}

				ApplyRackColor(__result, rackColor);
			}
		}

		[HarmonyPatch(typeof(Rack), nameof(Rack.ButtonUnmountRack))]
		private static class RackButtonUnmountPatch
		{
			private static void Prefix(Rack __instance)
			{
				CaptureUnmountState(__instance);
			}
		}

		[HarmonyPatch(typeof(UsableObject), nameof(UsableObject.InteractOnClick))]
		private static class RackBoxPickupPatch
		{
			private static void Prefix(UsableObject __instance)
			{
				if (HasPendingUnmountState() && IsRackBox(__instance))
				{
					ApplyPendingUnmountState(__instance);
				}

				RefreshBoxColor(__instance);
			}
		}

		[HarmonyPatch(typeof(Interact), nameof(Interact.OnLoad))]
		private static class InteractOnLoadPatch
		{
			private static void Postfix(Interact __instance, InteractObjectData data)
			{
				UsableObject boxedRack = __instance.TryCast<UsableObject>();
				if (boxedRack == null)
				{
					return;
				}

				RefreshBoxColor(boxedRack, data);
			}
		}
	}
}
