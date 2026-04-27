using System;
using System.Collections;
using System.Collections.Generic;
using PLAYERTWO.ARPGProject.Controllers;
using UnityEngine;
using UnityEngine.UI;

namespace PLAYERTWO.ARPGProject
{    
    public struct PluginToggleEvent
    {
        public string pluginId;
        public bool enabled;

        public PluginToggleEvent(string pluginId, bool enabled)
        {
            this.pluginId = pluginId;
            this.enabled = enabled;
        }
    }

    public static class EventBus
    {
        public const string EntityAttacked = nameof(EntityAttacked);
        public const string FactionHostilityChanged = nameof(FactionHostilityChanged);
        public const string QuestCompleted = nameof(QuestCompleted);
        public const string ItemLootInstantiated = nameof(ItemLootInstantiated);
        public const string RarityItemSet = nameof(RarityItemSet);
        public const string InspectorItemNameUpdated = nameof(InspectorItemNameUpdated);
        public const string InventoryItemAdded = nameof(InventoryItemAdded);
        public const string InventoryAutoSortRequested = nameof(InventoryAutoSortRequested);
        public const string ItemInspectorShown = nameof(ItemInspectorShown);
        public const string ItemInspectorHidden = nameof(ItemInspectorHidden);
        public const string ItemInspectorUpdated = nameof(ItemInspectorUpdated);
        public const string MoveTo = nameof(MoveTo);
        public const string CollectibleGUINameInstantiated = nameof(CollectibleGUINameInstantiated);
        public const string MouseCameraRotateDelta = nameof(MouseCameraRotateDelta);
        public const string InfinityWardrobeRefreshRequested = nameof(InfinityWardrobeRefreshRequested);
        public const string InfinityWardrobeApplied = nameof(InfinityWardrobeApplied);
        public const string QuestWindowUpdateRequested = nameof(QuestWindowUpdateRequested);
        public const string ArcDropRequested = nameof(ArcDropRequested);


        private static readonly Dictionary<string, Action<object>> s_events = new();
        private static readonly Dictionary<Type, Delegate> m_handlers =
            new Dictionary<Type, Delegate>();



        /// <summary>
        /// Fired whenever one <see cref="Entity"/> attacks another.
        /// Parameters are (attacker, defender).
        /// </summary>
        public static event Action<Entity, Entity> Attack;
        public static event Action<ItemInstance> ItemLootInstantiatedEvent;
        public static event Action<CollectibleItem> RarityItemSetEvent;
        public static event Action<ItemInstance, Text> InspectorItemNameUpdatedEvent;
        public static event Action<GUIItemInspector, ItemInstance, GUIItem> ItemInspectorShownEvent;
        public static event Action<GUIItemInspector, ItemInstance, GUIItem> ItemInspectorHiddenEvent;
        public static event Action<GUIItemInspector, ItemInstance, GUIItem> ItemInspectorUpdatedEvent;
        public static event Action<ItemInstance, Inventory> InventoryItemAddedEvent;
        public static event Action<Inventory> InventoryAutoSortRequestedEvent;
        public static event Action<object> MoveToRequested;
        public static event Action<Entity> InfinityWardrobeRefreshEvent;
        public static event Action<Entity> InfinityWardrobeAppliedEvent;
        public static event Action<Collectible, GUICollectibleName> CollectibleGUINameInstantiatedEvent;

         public static void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
                return;

            var key = typeof(T);
            Delegate existing;

            if (m_handlers.TryGetValue(key, out existing))
                m_handlers[key] = Delegate.Combine(existing, handler);
            else
                m_handlers[key] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
                return;

            var key = typeof(T);
            Delegate existing;

            if (!m_handlers.TryGetValue(key, out existing))
                return;

            var updated = Delegate.Remove(existing, handler);

            if (updated == null)
                m_handlers.Remove(key);
            else
                m_handlers[key] = updated;
        }

        public static void Publish<T>(T evt)
        {
            Delegate handlers;

            if (!m_handlers.TryGetValue(typeof(T), out handlers))
                return;

            var action = handlers as Action<T>;

            if (action != null)
                action.Invoke(evt);
        }

        public static void Clear()
        {
            m_handlers.Clear();
        }



        public static bool RaiseQuestWindowUpdate(GUIQuestWindow questWindow, Quest quest)
        {
            bool handled = false;

            Publish(
                QuestWindowUpdateRequested,
                new object[] { questWindow, quest, (Action<bool>)(flag => handled |= flag) }
            );

            return handled;
        }

        public static bool RaiseArcDropRequested(GUI gui, GUIItem guiItem, Entity entity, Action onDropCompleted, Action onDropFailed)
        {
            bool handled = false;
            Func<bool> isHandled = () => handled;

            Publish(
                ArcDropRequested,
                new object[]
                {
                    gui,
                    guiItem,
                    entity,
                    onDropCompleted,
                    onDropFailed,
                    (Action<bool>)(flag => handled |= flag),
                    isHandled,
                }
            );

            return handled;
        }

        public static void RaiseMouseCameraRotateDelta(float delta)
        {
            Publish(MouseCameraRotateDelta, delta);
        }

        public static void RaiseInfinityWardrobeRefresh(Entity entity)
        {
            InfinityWardrobeRefreshEvent?.Invoke(entity);
            Publish(InfinityWardrobeRefreshRequested, entity);
        }

        public static void RaiseInfinityWardrobeApplied(Entity entity)
        {
            InfinityWardrobeAppliedEvent?.Invoke(entity);
            Publish(InfinityWardrobeApplied, entity);
        }


        public static void Subscribe(string eventName, Action<object> handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
                return;

            if (s_events.TryGetValue(eventName, out var existing))
                s_events[eventName] = existing + handler;
            else
                s_events[eventName] = handler;
        }

        public static void Unsubscribe(string eventName, Action<object> handler)
        {
            if (string.IsNullOrEmpty(eventName) || handler == null)
                return;

            if (s_events.TryGetValue(eventName, out var existing))
            {
                existing -= handler;
                if (existing == null)
                    s_events.Remove(eventName);
                else
                    s_events[eventName] = existing;
            }
        }

        public static void moveTo(Entity entity, Vector3 position)
        {
            if (entity == null)
                return;
            // Publish the MoveTo event
            MoveToRequested?.Invoke(new { entity, position });
            // Call the extras component to handle the move
            var extras = entity.GetComponent("CharacterControllerExtras");
            if (extras == null)
                return;

            var moveTo = extras.GetType().GetMethod("MoveTo", new[] { typeof(Vector3) });
            moveTo?.Invoke(extras, new object[] { position });
        }

        public static void isItAllowedToMove(Entity m_entity, Transform m_interactive)
        {
            // Check if the move is allowed by plugins or other systems
            bool moveAllowed = true;
            EventBus.Publish(
                "CharacterController.MoveTo",
                new object[]
                {
                        m_entity,
                        m_interactive.transform.position,
                        (System.Action<bool>)(allowed => moveAllowed = allowed),
                }
            );
        }

        public static void allowMoveToPoint(Entity m_entity, RaycastHit hit)
        {
            if (m_entity == null)
                return;

            var destination = hit.point;

            var extras = m_entity.GetComponent<CharacterControllerExtras>();

            // Ignore tiny command noise from terrain raycasts while the mouse is held.
            // This threshold is configurable in CharacterControllerExtras.
            if (extras != null && extras.IsInsideCommandDistance(destination))
            {
                m_entity.StandStill();
                return;
            }

            bool callbackReceived = false;
            bool movedByPlugin = false;

            EventBus.Publish(
                "CharacterController.MoveTo",
                new object[]
                {
                    m_entity,
                    destination,
                    (System.Action<bool>)(moved =>
                    {
                        callbackReceived = true;
                        movedByPlugin = moved;
                    }),
                }
            );

            // If no plugin handled the request, keep backward-compatible core movement.
            if (!callbackReceived)
            {
                m_entity.MoveTo(destination);
                return;
            }

            if (!movedByPlugin)
                m_entity.StandStill();
        }

        public static void allowMoveToTarget(Entity m_entity, Transform m_target)
        {
            if (m_entity == null || m_target == null)
                return;

            var destination = m_target.position;

            bool callbackReceived = false;
            bool movedByPlugin = false;

            EventBus.Publish(
                "CharacterController.MoveTo",
                new object[]
                {
                    m_entity,
                    destination,
                    (System.Action<bool>)(moved =>
                    {
                        callbackReceived = true;
                        movedByPlugin = moved;
                    }),
                }
            );

            // If no plugin handled the request, keep backward-compatible core movement.
            if (!callbackReceived)
            {
                m_entity.MoveTo(destination);
                return;
            }

            if (!movedByPlugin)
                m_entity.StandStill();
        }



        public static void Publish(string eventName, object payload = null)
        {
            if (string.IsNullOrEmpty(eventName))
                return;

            if (s_events.TryGetValue(eventName, out var handlers))
                handlers.Invoke(payload);
        }

        public static void RaiseAttack(Entity attacker, Entity defender)
        {
            Attack?.Invoke(attacker, defender);
            Publish(EntityAttacked, new object[] { attacker, defender });
        }

        public static void RaiseItemLootInstantiated(ItemInstance item)
        {
            ItemLootInstantiatedEvent?.Invoke(item);
            Publish(ItemLootInstantiated, item);
        }

        public static void RaiseRarityItemSet(CollectibleItem rarity)
        {
            RarityItemSetEvent?.Invoke(rarity);
            Publish(RarityItemSet, rarity);
        }

        public static void RaiseInspectorItemNameUpdated(ItemInstance item, Text label)
        {
            InspectorItemNameUpdatedEvent?.Invoke(item, label);
            Publish(InspectorItemNameUpdated, new object[] { item, label });
        }

        public static void RaiseItemInspectorShown(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            ItemInspectorShownEvent?.Invoke(inspector, item, guiItem);
            Publish(ItemInspectorShown, new object[] { inspector, item, guiItem });
        }

        public static void RaiseItemInspectorHidden(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            ItemInspectorHiddenEvent?.Invoke(inspector, item, guiItem);
            Publish(ItemInspectorHidden, new object[] { inspector, item, guiItem });
        }

        public static void RaiseItemInspectorUpdated(GUIItemInspector inspector, ItemInstance item, GUIItem guiItem)
        {
            ItemInspectorUpdatedEvent?.Invoke(inspector, item, guiItem);
            Publish(ItemInspectorUpdated, new object[] { inspector, item, guiItem });
        }
        public static void RaiseInventoryItemAdded(ItemInstance item, Inventory inventory)
        {
            InventoryItemAddedEvent?.Invoke(item, inventory);
            Publish(InventoryItemAdded, new object[] { item, inventory });
        }
        public static void RaiseInventoryAutoSortRequested(Inventory inventory)
        {
            InventoryAutoSortRequestedEvent?.Invoke(inventory);
            Publish(InventoryAutoSortRequested, inventory);
        }
        public static void RaiseCollectibleGUINameInstantiated(Collectible collectible, GUICollectibleName gui)
        {
            CollectibleGUINameInstantiatedEvent?.Invoke(collectible, gui);
            Publish(CollectibleGUINameInstantiated, new object[] { collectible, gui });
        }
    }
}
