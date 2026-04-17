using System;
using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(InstaRack.InstaRackMod), "InstaRack", "1.0.4", "derrick")]
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
		private static bool hasPendingUnmountPosition;
		private static Vector3 pendingUnmountPosition;
		private static int pendingUnmountTicket;

		private static bool IsPlacingRack()
		{
			return playerManager != null && playerManager.objectInHand == PlayerManager.ObjectInHand.Rack;
		}

		public override void OnInitializeMelon()
		{
			MelonCoroutines.Start(RefreshWorldRackBoxesLoop());
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

		private static bool TryGetRackColor(GameObject root, out Color color)
		{
			color = default;
			if (root == null)
			{
				return false;
			}

			foreach (Interact interact in root.GetComponentsInChildren<Interact>(true))
			{
				if (TryGetRackColor(interact, out color))
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
				if (rack == null || !TryGetRackColor(source, out Color rackColor))
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

				if (!TryGetRackColor(data, out Color rackColor) && !TryGetRackColor(fallback, out rackColor))
				{
					return false;
				}

				SetRackColor(fallback, rackColor);
				SyncRackColor(rack, rackColor);
				return ApplyRackColor(rack, rackColor);
			}

			private static bool ApplyBoxColor(GameObject boxedRack, Color rackColor)
			{
				return ApplyColor(boxedRack, rackColor, "BoxedRack");
			}

			private static bool RefreshBoxColor(UsableObject boxedRack)
			{
				if (!IsRackBox(boxedRack))
				{
					return false;
				}

				if (!TryGetRackColor(boxedRack, out Color rackColor)
					&& !TryGetRackColor(boxedRack.gameObject, out rackColor))
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

				if (!TryGetRackColor(data, out Color rackColor)
					&& !TryGetRackColor(boxedRack, out rackColor)
					&& !TryGetRackColor(boxedRack.gameObject, out rackColor))
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

			private static void RefreshWorldRackBoxes()
			{
				foreach (UsableObject usableObject in Resources.FindObjectsOfTypeAll<UsableObject>())
				{
					if (usableObject?.gameObject == null || !usableObject.gameObject.scene.IsValid())
					{
						continue;
					}

					RefreshBoxColor(usableObject);
				}
			}

			private static IEnumerator RefreshWorldRackBoxesLoop()
			{
				while (true)
				{
					RefreshWorldRackBoxes();
					yield return new WaitForSeconds(01f);
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
			hasPendingUnmountPosition = false;
			pendingUnmountPosition = default;
		}

		private static UsableObject FindPendingUnmountBox()
		{
			UsableObject heldRack = GetHeldRack();
			if (IsRackBox(heldRack))
			{
				return heldRack;
			}

			if (!hasPendingUnmountPosition)
			{
				return null;
			}

			UsableObject bestMatch = null;
			float bestDistanceSqr = float.MaxValue;
			foreach (UsableObject usableObject in Resources.FindObjectsOfTypeAll<UsableObject>())
			{
				if (!IsRackBox(usableObject) || usableObject.gameObject == null || !usableObject.gameObject.scene.IsValid())
				{
					continue;
				}

				float distanceSqr = (usableObject.transform.position - pendingUnmountPosition).sqrMagnitude;
				if (distanceSqr > 16f || distanceSqr >= bestDistanceSqr)
				{
					continue;
				}

				bestMatch = usableObject;
				bestDistanceSqr = distanceSqr;
			}

			return bestMatch;
		}

		private static IEnumerator WaitForUnmountBox(int ticket)
		{
			for (int frame = 0; frame < 180; frame++)
			{
				if (ticket != pendingUnmountTicket || !HasPendingUnmountState())
				{
					yield break;
				}

				UsableObject boxedRack = FindPendingUnmountBox();
				if (boxedRack != null)
				{
					ApplyPendingUnmountState(boxedRack);
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
			hasPendingUnmountPosition = true;
			pendingUnmountPosition = rackMount.transform.position;
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

				if (TryGetRackColor(heldRack, out Color rackColor))
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
					UsableObject boxedRack = __instance.TryCast<UsableObject>();
					if (boxedRack == null)
					{
						return;
					}

					RefreshBoxColor(boxedRack, data);
					MelonCoroutines.Start(RefreshBoxColorLater(boxedRack, data));
				}
			}

			[HarmonyPatch(typeof(UsableObject), nameof(UsableObject.OnLoad))]
			private static class UsableObjectOnLoadPatch
			{
				private static void Postfix(UsableObject __instance, InteractObjectData data)
				{
					if (!IsRackBox(__instance))
					{
						return;
					}

					RefreshBoxColor(__instance, data);
					MelonCoroutines.Start(RefreshBoxColorLater(__instance, data));
				}
			}

			[HarmonyPatch(typeof(UsableObject), "OnEnable")]
			private static class RackBoxOnEnablePatch
			{
				private static void Postfix(UsableObject __instance)
				{
					if (!IsRackBox(__instance))
					{
						return;
					}

					if (HasPendingUnmountState())
					{
						ApplyPendingUnmountState(__instance);
					}

					RefreshBoxColor(__instance);
					MelonCoroutines.Start(RefreshBoxColorLater(__instance));
				}
			}
	}
}
