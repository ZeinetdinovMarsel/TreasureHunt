using System.Collections.Generic;
using System.Text;
using TreasureHunt.Cameras;
using UnityEngine;
using Zenject;

namespace TreasureHunt.UI
{
    /// <summary>
    /// Debug-style overlay that:
    ///   • shows a top-left HUD with the active camera mode and the currently observed entity;
    ///   • draws a screen-space label above every agent / golem / treasure that lies within
    ///     <see cref="ProximityRange"/> of the rendering camera, listing the same fields that
    ///     <see cref="NetworkServer"/> serialises into the <c>WorldStateDto</c>.
    ///
    /// Implementation deliberately uses IMGUI: it keeps the integration zero-touch (no canvas
    /// to wire, no prefabs to edit) and it is a debug tool — the cost is negligible for
    /// the handful of entities that fit inside the 10-metre radius.
    /// </summary>
    public sealed class EntityInspectorHud : MonoBehaviour
    {
        private const float ProximityRange = 10f;
        private const float ProximityRangeSqr = ProximityRange * ProximityRange;

        private IActiveCameraProvider _cameraProvider;
        private ICameraModeService _modeService;
        private IAgentObserverService _observerService;
        private TeamBase[] _teams;
        private GolemGenerator _golems;
        private TreasureGenerator _treasures;

        private GUIStyle _labelStyle;
        private GUIStyle _hudStyle;
        private Texture2D _bgTexture;
        private readonly StringBuilder _builder = new StringBuilder(256);

        [Inject]
        public void Construct(
            IActiveCameraProvider cameraProvider,
            ICameraModeService modeService,
            IAgentObserverService observerService,
            TeamBase[] teams,
            GolemGenerator golems,
            TreasureGenerator treasures)
        {
            _cameraProvider = cameraProvider;
            _modeService = modeService;
            _observerService = observerService;
            _teams = teams;
            _golems = golems;
            _treasures = treasures;
        }

        private void OnGUI()
        {
            // OnGUI fires Layout + Repaint (and assorted input events) per frame. Our HUD uses
            // explicit GUI.Label rects, so the Layout pass would only re-do the same iteration
            // for nothing. Limiting to Repaint roughly halves the per-frame cost when there are
            // many entities to scan.
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();
            DrawCameraHud();
            DrawNearbyEntityLabels();
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
            {
                Destroy(_bgTexture);
                _bgTexture = null;
            }
        }

        private void EnsureStyles()
        {
            if (_bgTexture == null)
            {
                _bgTexture = new Texture2D(1, 1);
                _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
                _bgTexture.Apply();
            }

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 16,
                    padding = new RectOffset(10, 10, 8, 8),
                    richText = true,
                };
                _hudStyle.normal.background = _bgTexture;
                _hudStyle.normal.textColor = Color.white;
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    padding = new RectOffset(6, 6, 4, 4),
                    richText = true,
                    wordWrap = false,
                };
                _labelStyle.normal.background = _bgTexture;
                _labelStyle.normal.textColor = Color.white;
            }
        }

        private void DrawCameraHud()
        {
            _builder.Clear();
            _builder.Append("<b>Camera:</b> ").Append(_modeService.Mode.Value);

            if (_modeService.Mode.Value == CameraMode.Observer)
            {
                var agent = _observerService.CurrentAgent;
                _builder.Append("\n<b>Watching:</b> ");
                if (agent != null)
                {
                    _builder.Append("agent_").Append(SafeStr(agent.AgentId))
                        .Append(" (").Append(SafeStr(agent.TeamId)).Append(')')
                        .Append("  [")
                        .Append(_observerService.CurrentIndex.Value + 1)
                        .Append('/').Append(_observerService.Count.Value).Append(']');
                }
                else
                {
                    _builder.Append("—");
                }
                _builder.Append("\n<size=12>Q/E — switch agent · Space — free fly</size>");
            }
            else if (_modeService.Mode.Value == CameraMode.FlyCam)
            {
                _builder.Append("\n<size=12>Tab — minimap · Space — observer</size>");
            }
            else // TopDown
            {
                _builder.Append("\n<size=12>Tab — back to fly · WASD — pan · Wheel — zoom</size>");
            }

            var content = new GUIContent(_builder.ToString());
            Vector2 size = _hudStyle.CalcSize(content);
            size.x = Mathf.Max(size.x, 220f);
            GUI.Label(new Rect(12f, 12f, size.x, size.y + 4f), content, _hudStyle);
        }

        private void DrawNearbyEntityLabels()
        {
            Camera cam = _cameraProvider != null ? _cameraProvider.Active : Camera.main;
            if (cam == null) return;

            // TopDown uses hover instead of proximity — at 120m altitude nothing is "close",
            // and distance-based labels would clutter the minimap view anyway.
            if (_modeService.Mode.Value == CameraMode.TopDown)
            {
                DrawHoverInspector(cam);
                return;
            }

            Vector3 camPos = cam.transform.position;

            if (_teams != null)
            {
                foreach (var team in _teams)
                {
                    if (team == null) continue;
                    foreach (var agent in team.Objects)
                        DrawAgentLabel(cam, camPos, agent);
                }
            }

            if (_golems != null)
            {
                foreach (var go in _golems.Objects)
                    DrawGolemLabel(cam, camPos, go);
            }

            if (_treasures != null)
            {
                foreach (var go in _treasures.Objects)
                    DrawTreasureLabel(cam, camPos, go);
            }
        }

        private void DrawAgentLabel(Camera cam, Vector3 camPos, AgentBehaviour agent)
        {
            if (agent == null) return;

            Vector3 worldPos = agent.transform.position + Vector3.up * 2.5f;
            if (!IsInRange(camPos, worldPos)) return;

            _builder.Clear();
            _builder.Append("<b>Agent ").Append(SafeStr(agent.AgentId)).Append("</b>")
                .Append("\nteam: ").Append(SafeStr(agent.TeamId))
                .Append("\npos: ").Append(FormatPos(agent.transform.position))
                .Append("\nhasTreasure: ").Append(agent.HasTreasure)
                .Append("\nisStunned: ").Append(agent.IsStunned.Value);

            DrawLabelAtWorldPosition(cam, worldPos, _builder.ToString());
        }

        private void DrawGolemLabel(Camera cam, Vector3 camPos, GameObject host)
        {
            if (host == null) return;
            var ai = host.GetComponentInChildren<EnemyAI>(true);
            if (ai == null) return;

            Vector3 worldPos = ai.transform.position + Vector3.up * 3f;
            if (!IsInRange(camPos, worldPos)) return;

            _builder.Clear();
            _builder.Append("<b>Golem</b>")
                .Append("\nid: ").Append(host.GetInstanceID())
                .Append("\npos: ").Append(FormatPos(ai.transform.position))
                .Append("\nspeed: ").Append(ai.CurrentSpeed.ToString("0.0"))
                .Append("\nstate: ").Append(ai.CurrentState);

            DrawLabelAtWorldPosition(cam, worldPos, _builder.ToString());
        }

        private void DrawTreasureLabel(Camera cam, Vector3 camPos, GameObject host)
        {
            if (host == null) return;
            var item = host.GetComponent<WorldItem>();
            if (item == null) return;

            Vector3 worldPos = host.transform.position + Vector3.up * 1.2f;
            if (!IsInRange(camPos, worldPos)) return;

            _builder.Clear();
            _builder.Append("<b>Treasure</b>")
                .Append("\nid: ").Append(host.GetInstanceID())
                .Append("\npos: ").Append(FormatPos(host.transform.position))
                .Append("\nisPicked: ").Append(item.IsPicked);

            DrawLabelAtWorldPosition(cam, worldPos, _builder.ToString());
        }

        private static bool IsInRange(Vector3 camPos, Vector3 worldPos)
        {
            return (worldPos - camPos).sqrMagnitude <= ProximityRangeSqr;
        }

        private void DrawHoverInspector(Camera cam)
        {
            // OnGUI uses top-left origin; convert mouse to bottom-left screen for matching against
            // Camera.WorldToScreenPoint values.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            Vector2 mouseScreen = mouse.position.ReadValue();

            const float HoverRadiusPx = 28f;
            const float HoverRadiusSqr = HoverRadiusPx * HoverRadiusPx;

            // Iterate all entities, find the closest one to the mouse and draw a single panel
            // for it. Picking only one keeps the screen readable when icons overlap.
            float bestSqr = HoverRadiusSqr;
            string bestLabel = null;
            Vector3 bestScreen = default;

            if (_teams != null)
            {
                foreach (var team in _teams)
                {
                    if (team == null) continue;
                    foreach (var agent in team.Objects)
                    {
                        if (agent == null) continue;
                        if (TryHover(cam, mouseScreen, agent.transform.position + Vector3.up * 0.5f, out var screen, out float sqr) && sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            bestScreen = screen;
                            _builder.Clear();
                            BuildAgentLabel(agent);
                            bestLabel = _builder.ToString();
                        }
                    }
                }
            }

            if (_golems != null)
            {
                foreach (var go in _golems.Objects)
                {
                    if (go == null) continue;
                    var ai = go.GetComponentInChildren<EnemyAI>(true);
                    if (ai == null) continue;
                    if (TryHover(cam, mouseScreen, ai.transform.position + Vector3.up * 0.5f, out var screen, out float sqr) && sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestScreen = screen;
                        _builder.Clear();
                        BuildGolemLabel(go, ai);
                        bestLabel = _builder.ToString();
                    }
                }
            }

            if (_treasures != null)
            {
                foreach (var go in _treasures.Objects)
                {
                    if (go == null) continue;
                    if (TryHover(cam, mouseScreen, go.transform.position + Vector3.up * 0.5f, out var screen, out float sqr) && sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestScreen = screen;
                        _builder.Clear();
                        BuildTreasureLabel(go);
                        bestLabel = _builder.ToString();
                    }
                }
            }

            if (bestLabel != null)
                DrawLabelAtScreenPoint(bestScreen, bestLabel);
        }

        private static bool TryHover(Camera cam, Vector2 mouseScreen, Vector3 worldPos, out Vector3 screen, out float sqr)
        {
            screen = cam.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f) { sqr = float.PositiveInfinity; return false; }

            float dx = screen.x - mouseScreen.x;
            float dy = screen.y - mouseScreen.y;
            sqr = dx * dx + dy * dy;
            return true;
        }

        private void BuildAgentLabel(AgentBehaviour agent)
        {
            _builder.Append("<b>Agent ").Append(SafeStr(agent.AgentId)).Append("</b>")
                .Append("\nteam: ").Append(SafeStr(agent.TeamId))
                .Append("\npos: ").Append(FormatPos(agent.transform.position))
                .Append("\nhasTreasure: ").Append(agent.HasTreasure)
                .Append("\nisStunned: ").Append(agent.IsStunned.Value);
        }

        private void BuildGolemLabel(GameObject host, EnemyAI ai)
        {
            _builder.Append("<b>Golem</b>")
                .Append("\nid: ").Append(host.GetInstanceID())
                .Append("\npos: ").Append(FormatPos(ai.transform.position))
                .Append("\nspeed: ").Append(ai.CurrentSpeed.ToString("0.0"))
                .Append("\nstate: ").Append(ai.CurrentState);
        }

        private void BuildTreasureLabel(GameObject host)
        {
            var item = host.GetComponent<WorldItem>();
            _builder.Append("<b>Treasure</b>")
                .Append("\nid: ").Append(host.GetInstanceID())
                .Append("\npos: ").Append(FormatPos(host.transform.position));
            if (item != null) _builder.Append("\nisPicked: ").Append(item.IsPicked);
        }

        private void DrawLabelAtScreenPoint(Vector3 screen, string text)
        {
            var content = new GUIContent(text);
            Vector2 size = _labelStyle.CalcSize(content);
            float x = screen.x + 16f;
            float y = (Screen.height - screen.y) - size.y * 0.5f;
            // Keep the label inside the screen.
            x = Mathf.Clamp(x, 8f, Screen.width - size.x - 8f);
            y = Mathf.Clamp(y, 8f, Screen.height - size.y - 8f);
            GUI.Label(new Rect(x, y, size.x, size.y), content, _labelStyle);
        }

        private void DrawLabelAtWorldPosition(Camera cam, Vector3 worldPos, string text)
        {
            Vector3 screen = cam.WorldToScreenPoint(worldPos);
            if (screen.z <= 0f) return;

            var content = new GUIContent(text);
            Vector2 size = _labelStyle.CalcSize(content);
            // IMGUI uses screen coordinates with origin at top-left; Camera.WorldToScreenPoint
            // returns origin at bottom-left, so flip Y.
            float x = screen.x - size.x * 0.5f;
            float y = (Screen.height - screen.y) - size.y - 4f;
            GUI.Label(new Rect(x, y, size.x, size.y), content, _labelStyle);
        }

        private static string FormatPos(Vector3 v)
        {
            return v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0");
        }

        private static string SafeStr(string s) => string.IsNullOrEmpty(s) ? "?" : s;
    }
}
