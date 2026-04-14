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
		
		public static MelonLogger.Instance Logger { get; private set; }
		public static PlayerManager playerManager => PlayerManager.instance;

		public override void OnInitializeMelon()
		{
			Logger = LoggerInstance;

			HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

			Logger.Msg("InstaRack initialized.");
		}

		private static bool IsPlacingRack()
		{
			return playerManager != null && playerManager.objectInHand == PlayerManager.ObjectInHand.Rack;
		}

		private static bool InstantiateRackDirectly(RackMount rackMount)
		{
			if (rackMount == null || rackMount.isRackInstantiated)
			{
				return false;
			}

			InteractObjectData saveData = new(rackMount);
			GameObject rack = rackMount.InstantiateRack(saveData);
			if (rack == null)
			{
				return false;
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

			Logger.Msg($"Rack instantiated directly at {rackMount.uid}.");
			
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
					if (!InstantiateRackDirectly(__instance))
					{
						return true;
					}

					return false;
				}
				catch (Exception exception)
				{
					Logger.Warning($"Direct rack instantiation failed at {__instance.uid}: {exception.GetType().Name}: {exception.Message}");

					return true;
				}
			}
		}
	}
}
