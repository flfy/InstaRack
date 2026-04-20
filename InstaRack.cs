using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;	

[assembly: MelonInfo(typeof(InstaRack.InstaRackMod), "InstaRack", "1.0.5", "derrick")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace InstaRack
{
	public sealed class InstaRackMod : MelonMod
	{
		private const int RackColorMarkerA = 1229735501;
		private const int RackColorMarkerB = 1380011595;

		public static PlayerManager playerManager => PlayerManager.instance;

		private static Il2CppStructArray<float> pendingUnmountSaveValue;
		private static Il2CppStructArray<int> pendingUnmountSaveIntArray;
		private static Il2CppStructArray<int> pendingUnmountSaveIntArray2;
		private static bool hasPendingUnmountColor;
		private static Color pendingUnmountColor;
		private static int pendingUnmountTicket;
		private static readonly HashSet<int> trackedWorldRackBoxes = new();
		private static int pendingShopRefreshTicket;

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

		private static bool HasRackColorMarker(Il2CppStructArray<int> source)
		{
			return source != null
				&& source.Length >= 2
				&& source[source.Length - 2] == RackColorMarkerA
				&& source[source.Length - 1] == RackColorMarkerB;
		}

		private static Il2CppStructArray<int> EnsureRackColorMarker(Il2CppStructArray<int> source)
		{
			if (HasRackColorMarker(source))
			{
				return source;
			}

			if (source == null)
			{
				Il2CppStructArray<int> markerOnly = new(2);
				markerOnly[0] = RackColorMarkerA;
				markerOnly[1] = RackColorMarkerB;
				return markerOnly;
			}

			Il2CppStructArray<int> expanded = new(source.Length + 2);
			for (int i = 0; i < source.Length; i++)
			{
				expanded[i] = source[i];
			}

			expanded[expanded.Length - 2] = RackColorMarkerA;
			expanded[expanded.Length - 1] = RackColorMarkerB;
			return expanded;
		}

		private static bool GetRackColor(Interact source, out Color color)
		{
			color = default;
			if (source == null)
			{
				return false;
			}

			return GetMarkedRackColor(source.saveValue, source.saveIntArray2, out color)
				|| (source is UsableObject boxedRack && IsRackBox(boxedRack) && GetLooseRackColor(source.saveValue, out color));
		}

		private static bool GetRackColor(InteractObjectData data, out Color color)
		{
			color = default;
			return GetMarkedRackColor(data?.value, data?.saveIntArray2, out color);
		}

		private static bool GetRackColor(GameObject root, out Color color)
		{
			color = default;
			if (root == null)
			{
				return false;
			}

			foreach (Interact interact in root.GetComponentsInChildren<Interact>(true))
			{
				if (GetRackColor(interact, out color))
				{
					return true;
				}
			}

			return false;
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

		private static bool GetMarkedRackColor(Il2CppStructArray<float> values, Il2CppStructArray<int> markerSource, out Color color)
		{
			color = default;
			if (values == null || values.Length < 7 || !HasRackColorMarker(markerSource))
			{
				return false;
			}

			color = new Color(values[3], values[4], values[5], values[6]);
			return true;
		}

		private static bool GetLooseRackColor(Il2CppStructArray<float> values, out Color color)
		{
			color = default;
			if (values == null || values.Length < 7)
			{
				return false;
			}

			Color parsed = new(values[3], values[4], values[5], values[6]);
			if (!IsReasonableColor(parsed))
			{
				return false;
			}

			color = parsed;
			return true;
		}

		private static bool IsReasonableColor(Color color)
		{
			return color.r >= 0f && color.r <= 1f
				&& color.g >= 0f && color.g <= 1f
				&& color.b >= 0f && color.b <= 1f
				&& color.a > 0f && color.a <= 1f;
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
			target.saveIntArray2 = EnsureRackColorMarker(CloneArray(target.saveIntArray2));
		}

		private static bool GetMaterialColor(GameObject root, string materialName, out Color color)
		{
			color = default;
			if (root == null)
			{
				return false;
			}

			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
			{
				if (renderer == null)
				{
					continue;
				}

				foreach (Material material in renderer.materials)
				{
					if (material == null || !material.name.Contains(materialName, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (material.HasProperty("_BaseColor"))
					{
						color = material.GetColor("_BaseColor");
						return true;
					}

					if (material.HasProperty("_Color"))
					{
						color = material.color;
						return true;
					}
				}
			}

			return false;
		}

		private static bool GetVisualRackColor(GameObject root, out Color color)
		{
			return GetMaterialColor(root, "BrushedAluminiumRack", out color)
				|| GetMaterialColor(root, "BoxedRack", out color);
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

			private static void SyncRackColor(GameObject root, Color color)
			{
				if (root == null)
				{
					return;
				}

				foreach (Interact interact in root.GetComponentsInChildren<Interact>(true))
				{
					SetRackColor(interact, color);
				}
			}

			private static bool ApplyColor(GameObject root, Color color, string materialName)
			{
				if (root == null)
				{
					return false;
				}

				bool changedAnyRenderer = false;
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

						changedRenderer |= TrySetMaterialColor(material, color);
					}

					if (changedRenderer)
					{
						renderer.materials = materials;
						changedAnyRenderer = true;
					}
				}

				return changedAnyRenderer;
			}

			private static bool TrySetMaterialColor(Material material, Color color)
			{
				if (material == null)
				{
					return false;
				}

				bool changed = false;
				if (material.HasProperty("_Color"))
				{
					material.color = color;
					changed = true;
				}

				if (material.HasProperty("_BaseColor"))
				{
					material.SetColor("_BaseColor", color);
					changed = true;
				}

				return changed;
			}

			private static bool ApplyColorToAllRenderers(GameObject root, Color color)
			{
				if (root == null)
				{
					return false;
				}

				bool changedAnyRenderer = false;
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
						changedRenderer |= TrySetMaterialColor(materials[i], color);
					}

					if (changedRenderer)
					{
						renderer.materials = materials;
						changedAnyRenderer = true;
					}
				}

				return changedAnyRenderer;
			}

			private static bool ApplyRackColor(GameObject rack, Color rackColor)
			{
				return ApplyColor(rack, rackColor, "BrushedAluminiumRack");
			}

			private static bool RefreshRackColor(GameObject rack, Interact source)
			{
				if (rack == null)
				{
					return false;
				}

				if (!GetRackColor(source, out Color rackColor)
					&& !GetVisualRackColor(rack, out rackColor))
				{
					return false;
				}

				SetRackColor(source, rackColor);
				SyncRackColor(rack, rackColor);
				return ApplyRackColor(rack, rackColor);
			}

			private static bool RefreshRackColor(GameObject rack, InteractObjectData data, Interact fallback)
			{
				if (rack == null)
				{
					return false;
				}

				if (!GetRackColor(data, out Color rackColor)
					&& !GetRackColor(fallback, out rackColor)
					&& !GetVisualRackColor(rack, out rackColor))
				{
					return false;
				}

				SetRackColor(fallback, rackColor);
				SyncRackColor(rack, rackColor);
				return ApplyRackColor(rack, rackColor);
			}

			private static bool ApplyBoxColor(GameObject boxedRack, Color rackColor)
			{
				return ApplyColor(boxedRack, rackColor, "BoxedRack")
					|| ApplyColorToAllRenderers(boxedRack, rackColor);
			}

			private static bool RefreshBoxColor(UsableObject boxedRack)
			{
				if (!IsRackBox(boxedRack))
				{
					return false;
				}

				if (!GetRackColor(boxedRack, out Color rackColor)
					&& !GetRackColor(boxedRack.gameObject, out rackColor)
					&& !GetVisualRackColor(boxedRack.gameObject, out rackColor))
				{
					return false;
				}

				SetRackColor(boxedRack, rackColor);
				SyncRackColor(boxedRack.gameObject, rackColor);
				return ApplyBoxColor(boxedRack.gameObject, rackColor);
			}

			private static bool RefreshBoxColor(UsableObject boxedRack, InteractObjectData data)
			{
				if (!IsRackBox(boxedRack))
				{
					return false;
				}

				if (!GetRackColor(data, out Color rackColor)
					&& !GetLooseRackColor(data?.value, out rackColor)
					&& !GetRackColor(boxedRack, out rackColor)
					&& !GetRackColor(boxedRack.gameObject, out rackColor)
					&& !GetVisualRackColor(boxedRack.gameObject, out rackColor))
				{
					return false;
				}

				SetRackColor(boxedRack, rackColor);
				SyncRackColor(boxedRack.gameObject, rackColor);
				return ApplyBoxColor(boxedRack.gameObject, rackColor);
			}

			private static IEnumerator RefreshRackColorLater(GameObject rack, Interact source, int maxFrames = 180)
			{
				for (int frame = 0; frame < maxFrames; frame++)
				{
					if (rack == null || source == null)
					{
						yield break;
					}

					if (RefreshRackColor(rack, source))
					{
						yield break;
					}

					yield return null;
				}
			}

			private static IEnumerator RefreshRackColorLater(GameObject rack, InteractObjectData data, Interact fallback, int maxFrames = 180)
			{
				for (int frame = 0; frame < maxFrames; frame++)
				{
					if (rack == null)
					{
						yield break;
					}

					if (RefreshRackColor(rack, data, fallback))
					{
						yield break;
					}

					yield return null;
				}
			}

			private static IEnumerator RefreshBoxColorLater(UsableObject boxedRack, int maxFrames = 180)
			{
				for (int frame = 0; frame < maxFrames; frame++)
				{
					if (boxedRack == null)
					{
						yield break;
					}

					if (RefreshBoxColor(boxedRack))
					{
						yield break;
					}

					yield return null;
				}
			}

			private static IEnumerator RefreshBoxColorLater(UsableObject boxedRack, InteractObjectData data, int maxFrames = 180)
			{
				for (int frame = 0; frame < maxFrames; frame++)
				{
					if (boxedRack == null)
					{
						yield break;
					}

					if (RefreshBoxColor(boxedRack, data))
					{
						yield break;
					}

					yield return null;
				}
			}

		private static void RefreshNewWorldRackBoxes()
		{
			foreach (UsableObject usableObject in Resources.FindObjectsOfTypeAll<UsableObject>())
			{
				if (usableObject?.gameObject == null || !usableObject.gameObject.scene.IsValid() || !IsRackBox(usableObject))
				{
					continue;
				}

				int instanceId = usableObject.gameObject.GetInstanceID();
				if (!trackedWorldRackBoxes.Add(instanceId))
				{
					continue;
				}

				RefreshBoxColor(usableObject);
				MelonCoroutines.Start(RefreshBoxColorLater(usableObject, 30));
			}
		}

		private static IEnumerator RefreshNewWorldRackBoxesForFrames(int ticket, int maxFrames = 12)
		{
			for (int frame = 0; frame < maxFrames; frame++)
			{
				if (ticket != pendingShopRefreshTicket)
				{
					yield break;
				}

				RefreshNewWorldRackBoxes();
				yield return null;
			}
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
			hasPendingUnmountColor = GetRackColor(rackMount, out pendingUnmountColor)
				|| GetVisualRackColor(rack?.gameObject, out pendingUnmountColor);
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
					SyncRackColor(boxedRack.gameObject, pendingUnmountColor);
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

				if (GetRackColor(heldRack, out Color rackColor)
					|| GetVisualRackColor(heldRack?.gameObject, out rackColor))
				{
					SetRackColor(rackMount, rackColor);
					SyncRackColor(rack, rackColor);
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

					RefreshRackColor(__result, saveData, __instance);
					MelonCoroutines.Start(RefreshRackColorLater(__result, saveData, __instance));
				}
			}

			[HarmonyPatch(typeof(RackMount), nameof(RackMount.OnLoad))]
			private static class RackMountOnLoadPatch
			{
				private static void Postfix(RackMount __instance)
				{
					Rack rack = __instance.GetComponentInChildren<Rack>(true);
					if (rack == null)
					{
						return;
					}

					RefreshRackColor(rack.gameObject, __instance);
					MelonCoroutines.Start(RefreshRackColorLater(rack.gameObject, __instance));
				}
			}

			[HarmonyPatch(typeof(Rack), nameof(Rack.OnLoad))]
			private static class RackOnLoadPatch
			{
				private static void Postfix(Rack __instance)
				{
					RefreshRackColor(__instance.gameObject, __instance.rackMount);
					MelonCoroutines.Start(RefreshRackColorLater(__instance.gameObject, __instance.rackMount));
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
					UsableObject boxedRack = __instance.Cast<UsableObject>();
					if (boxedRack == null)
					{
						return;
					}

					RefreshBoxColor(boxedRack, data);
					MelonCoroutines.Start(RefreshBoxColorLater(boxedRack, data));
				}
			}

			[HarmonyPatch(typeof(ComputerShop), "ApplyColorToSpawnedItem")]
			private static class ComputerShopApplyColorToSpawnedItemPatch
			{
				private static void Postfix(PlayerManager.ObjectInHand __2)
				{
					if (__2 != PlayerManager.ObjectInHand.Rack)
					{
						return;
					}

					pendingShopRefreshTicket++;
					MelonCoroutines.Start(RefreshNewWorldRackBoxesForFrames(pendingShopRefreshTicket));
				}
			}

	}
}
