using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(InstaRack.InstaRackMod), "InstaRack", "1.0.0", "derrick")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace InstaRack
{
	public sealed class InstaRackMod : MelonMod
	{
		public const string ModName = "InstaRack";

		public override void OnInitializeMelon()
		{
			HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
		}

		private static bool IsPlacingRack(PlayerManager playerManager)
		{
			return playerManager != null && playerManager.objectInHand == PlayerManager.ObjectInHand.Rack;
		}

		private static void RestorePlayerControl(PlayerManager playerManager)
		{
			if (playerManager == null)
			{
				return;
			}

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
		}

		private static void ClearRackBoxFromHands(PlayerManager playerManager)
		{
			if (!IsPlacingRack(playerManager))
			{
				return;
			}

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
		}

		[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.WaitForActionToFinish))]
		private static class PlayerManagerWFAPrefix
		{
			private static void Prefix(PlayerManager __instance, ref float _time)
			{
				if (!IsPlacingRack(__instance))
				{
					return;
				}

				_time = 0f;
			}
		}

		[HarmonyPatch(typeof(RackMount), nameof(RackMount.InteractOnClick))]
		private static class RackMountPostfix
		{
			private static void Postfix()
			{
				PlayerManager playerManager = PlayerManager.instance;
				if (!IsPlacingRack(playerManager))
				{
					return;
				}

				ClearRackBoxFromHands(playerManager);
				RestorePlayerControl(playerManager);
			}
		}
	}
}
