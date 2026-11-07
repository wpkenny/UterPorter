using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace UterPorter
{
    /// <summary>The porter: turns up beside the player, puts them on his back (the same attaching the game uses for
    /// chairs and riding a lox), runs them home or to their gravestone along a route planned over land, then runs
    /// off and vanishes. Jump climbs off at any time.</summary>
    public class Uter : MonoBehaviour
    {
        public static readonly List<Uter> All = new List<Uter>();

        /// <summary>A ride asked for before he'd arrived: he gives it as soon as he's here.</summary>
        public static Destination? Pending;

        private const string ZdoRider = "uter_rider";   // the player he came for (only their game runs him)

        private ZNetView _nview;
        private Character _character;
        private BaseAI _ai;
        private Transform _seat;

        private static readonly MethodInfo MoveTo =
            AccessTools.Method(typeof(BaseAI), "MoveTo", new[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) });

        // ---------------- who he's for ----------------

        public void SetRider(Player p)
        {
            if (_nview && _nview.IsValid() && p) _nview.GetZDO().Set(ZdoRider, p.GetPlayerID());
        }

        private long RiderId => _nview && _nview.IsValid() ? _nview.GetZDO().GetLong(ZdoRider, 0L) : 0L;

        public bool IsFor(Player p) => p && RiderId == p.GetPlayerID();

        // ---------------- lifecycle ----------------

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _ai = GetComponent<BaseAI>();
            All.Add(this);
        }

        private void OnDestroy()
        {
            All.Remove(this);
            if (_carrying) ReleaseRider();
        }

        private void Start()
        {
            if (_character) _character.m_name = Plugin.PorterName.Value;
            _leaveAt = Time.time + 30f;
            if (!_nview || !_nview.IsValid() || !_nview.IsOwner()) return;
            var ride = Pending;
            Pending = null;
            if (ride.HasValue) StartCoroutine(BeginSoon(ride.Value));
            else if (RiderId == 0L) Vanish();   // left over from an old session: nobody to carry
        }

        private IEnumerator BeginSoon(Destination to)
        {
            yield return new WaitForSeconds(0.5f);   // let him land
            Say(Pick(Lines.Hello));
            Begin(to);
        }

        private void Update()
        {
            if (!_nview || !_nview.IsValid()) return;
            if (!_nview.IsOwner())
            {
                // Only the game of the player he came for runs him.
                if (IsFor(Player.m_localPlayer)) _nview.ClaimOwnership();
                else return;
            }
            UpdateRide();
            if (_job == null && Time.time > _leaveAt) Leave();
        }

        private float _leaveAt;

        // ---------------- the ride ----------------

        private bool _carrying;
        private float _normalRunSpeed = -1f;

        /// <summary>Give the player a ride, or say why he can't (and go).</summary>
        public void Begin(Destination to)
        {
            var player = Player.m_localPlayer;
            if (!player) return;
            SetRider(player);
            _leaving = false;   // called back while heading off: turn round
            var problem = Problem(player, to, out var dest);
            if (problem != null) { Say(problem); Leave(); return; }
            StartJob(RideJob(player, dest, to));
        }

        private static string Problem(Player player, Destination to, out Vector3 dest)
        {
            dest = Vector3.zero;
            if (player.transform.position.y > 3000f) return "Not down here. Get us outside first.";
            if (player.IsAttached()) return "You're already sat on something.";
            if (to == Destination.Home)
            {
                if (!Home.TryGet(player, out dest))
                    return "Home? You haven't got a bed. Sleep in one, or tell me \"this is home\" somewhere.";
            }
            else
            {
                if (!NearestGrave(player, out dest)) return "You've no gravestone out there. Lucky you.";
                if (dest.y > 3000f) return "Your gravestone's down in a dungeon. I'm not running you down there.";
            }
            if (Vector3.Distance(Flat(player.transform.position), Flat(dest)) < 10f)
                return to == Destination.Home ? "We're already home." : "Your gravestone's right here.";
            return null;
        }

        /// <summary>The player's nearest gravestone, wherever it is in the world (loaded or not).</summary>
        private static bool NearestGrave(Player player, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (ZDOMan.instance == null) return false;
            var zdos = new List<ZDO>();
            int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative("Player_tombstone", zdos, ref index)) { }
            long me = player.GetPlayerID();
            var mine = zdos.Where(z => z != null && z.GetLong(ZDOVars.s_owner, 0L) == me)
                .OrderBy(z => Vector3.Distance(z.GetPosition(), player.transform.position))
                .FirstOrDefault();
            if (mine == null) return false;
            pos = mine.GetPosition();
            return true;
        }

        private IEnumerator RideJob(Player player, Vector3 dest, Destination to)
        {
            string where = to == Destination.Home ? "home" : "your gravestone";
            yield return GoTo(player.transform.position, 1.5f);
            if (!player || player.IsDead() || player.IsAttached()) { Leave(); yield break; }

            transform.rotation = Quaternion.LookRotation(Flat(dest - transform.position).normalized);
            Seat.localPosition = new Vector3(0f, Plugin.SeatHeight.Value, -Plugin.SeatBehind.Value);
            player.AttachStart(Seat, gameObject, true, false, false, "attach_lox", new Vector3(0f, 0.3f, -1f));
            _carrying = true;
            if (_character)
            {
                _normalRunSpeed = _character.m_runSpeed;
                _character.m_runSpeed *= Mathf.Max(0.5f, Plugin.SpeedBoost.Value);
            }
            Plugin.ShowMessage("Press Jump to climb off.");
            Grumble(Lines.PickUp);

            // Work out the whole way there over land first, then follow it.
            var route = new RoutePlanner.Result();
            yield return RoutePlanner.Plan(transform.position, dest, route);
            Plugin.Log.LogInfo($"Route to {where}: {(route.Found ? $"{route.Path.Count} waypoints" : "none found")}");
            int next = 0, noProgress = 0, replans = 0;

            while (_carrying)
            {
                if (!player || player.IsDead() || !player.IsAttached()) break;
                if (Vector3.Distance(Flat(transform.position), Flat(dest)) < 8f)
                {
                    Say(to == Destination.Home ? Pick(Lines.Home) : Pick(Lines.Grave));
                    ReleaseRider();
                    Leave();
                    yield break;
                }
                if (!route.Found)
                {
                    Say($"I can't find a way to {where} over land from here. You'll have to swim or sail for it.");
                    ReleaseRider();
                    Leave();
                    yield break;
                }
                if (Time.time >= _nextGrumble) Grumble(Lines.Riding);

                while (next < route.Path.Count - 1 && Vector3.Distance(Flat(transform.position), Flat(route.Path[next])) < 5f) next++;
                var waypoint = route.Path[Mathf.Min(next, route.Path.Count - 1)];

                var before = transform.position;
                yield return GoTo(waypoint, 3f);
                if (Vector3.Distance(Flat(before), Flat(transform.position)) > 2f) { noProgress = 0; continue; }

                // Not getting anywhere (a wall, a fence, terrain the plan didn't know about): plan again from here.
                if (++noProgress < 2) continue;
                if (++replans > 3)
                {
                    Say("I'm stuck, I can't get past here. Off you get.");
                    ReleaseRider();
                    Leave();
                    yield break;
                }
                route = new RoutePlanner.Result();
                yield return RoutePlanner.Plan(transform.position, dest, route);
                next = 0; noProgress = 0;
            }

            // They got off on their own (or something went wrong): off he goes.
            if (_carrying) { Say(Pick(Lines.OffEarly)); ReleaseRider(); }
            Leave();
        }

        private Transform Seat
        {
            get
            {
                if (_seat) return _seat;
                _seat = new GameObject("uter_seat").transform;
                _seat.SetParent(transform, false);
                _seat.localRotation = Quaternion.identity;
                return _seat;
            }
        }

        /// <summary>Every frame: Jump climbs off.</summary>
        private void UpdateRide()
        {
            if (!_carrying) return;
            var player = Player.m_localPlayer;
            if (!player || player.IsDead()) { ReleaseRider(); Leave(); return; }
            if (ZInput.GetButtonDown("Jump") || ZInput.GetButtonDown("JoyJump"))
            {
                Say(Pick(Lines.OffEarly));
                ReleaseRider();
                Leave();
            }
        }

        private void ReleaseRider()
        {
            if (!_carrying) return;
            _carrying = false;
            if (_character && _normalRunSpeed > 0f) _character.m_runSpeed = _normalRunSpeed;
            _normalRunSpeed = -1f;
            var player = Player.m_localPlayer;
            if (player && player.IsAttached()) player.AttachStop();
        }

        // ---------------- leaving ----------------

        private bool _leaving;

        /// <summary>Off he goes: runs away from the player and, once well out of sight, is gone until called again.</summary>
        private void Leave()
        {
            if (_leaving) return;
            _leaving = true;
            Say(Pick(Lines.Farewell));
            StartJob(LeaveJob());
        }

        private IEnumerator LeaveJob()
        {
            float giveUp = Time.time + 30f;
            for (int leg = 0; leg < 6 && Time.time < giveUp; leg++)
            {
                var player = Player.m_localPlayer;
                if (!player || Vector3.Distance(transform.position, player.transform.position) > 45f) break;
                var away = Flat(transform.position - player.transform.position);
                if (away.sqrMagnitude < 0.01f) away = -player.transform.forward;
                var spot = transform.position + away.normalized * 20f;
                if (Heightmap.GetHeight(spot, out float h)) spot.y = h;
                yield return GoTo(spot, 3f);
            }
            Vanish();
        }

        private void Vanish()
        {
            ReleaseRider();
            if (_nview && _nview.IsValid())
            {
                if (!_nview.IsOwner()) _nview.ClaimOwnership();
                _nview.Destroy();
            }
            else Destroy(gameObject);
        }

        // ---------------- jobs ----------------

        private Coroutine _job;
        private int _jobGen;

        /// <summary>Run one job at a time. Nested steps are stepped here, in one coroutine, so starting a new job
        /// really stops everything the old one was doing (Unity would otherwise keep nested steps running).</summary>
        private void StartJob(IEnumerator routine)
        {
            if (_job != null) StopCoroutine(_job);
            _moveTarget = null;
            int gen = ++_jobGen;
            _job = StartCoroutine(RunJob(routine, gen));
        }

        private IEnumerator RunJob(IEnumerator routine, int gen)
        {
            var steps = new Stack<IEnumerator>();
            steps.Push(routine);
            while (steps.Count > 0)
            {
                var top = steps.Peek();
                bool more;
                try { more = top.MoveNext(); }
                catch (System.Exception e) { Plugin.Log.LogError("Porter job failed: " + e); break; }
                if (gen != _jobGen) yield break;
                if (!more) { steps.Pop(); continue; }
                if (top.Current is IEnumerator inner) { steps.Push(inner); continue; }
                yield return top.Current;
                if (gen != _jobGen) yield break;
            }
            if (gen == _jobGen) _job = null;
        }

        // ---------------- moving ----------------

        private Vector3? _moveTarget;
        private float _moveDist = 1.5f;
        private bool _arrived;

        /// <summary>Called instead of the normal creature AI (see PorterMovementPatch).</summary>
        public void DriveMovement(float dt)
        {
            if (!_ai) return;
            if (_moveTarget == null) { _ai.StopMoving(); return; }

            bool run = Vector3.Distance(Flat(transform.position), Flat(_moveTarget.Value)) > 6f || _carrying || _leaving;
            bool done = MoveTo != null && (bool)MoveTo.Invoke(_ai, new object[] { dt, _moveTarget.Value, _moveDist, run });
            // The game's MoveTo also says "done" when it can't plan a route (ground not mapped that far ahead).
            // If he isn't actually there, head straight for it.
            var off = _moveTarget.Value - transform.position; off.y = 0f;
            if (done && off.magnitude > _moveDist + 0.5f)
            {
                done = false;
                _ai.MoveTowards(off.normalized, run);
            }

            if (done) { _arrived = true; _moveTarget = null; _ai.StopMoving(); }
            else JumpIfBlocked();
        }

        private const float StuckSeconds = 6f;

        /// <summary>Go to a point. Only if he makes no headway for a few seconds does he hop the rest of the way.</summary>
        private IEnumerator GoTo(Vector3 target, float dist)
        {
            if (Vector3.Distance(Flat(transform.position), Flat(target)) <= dist) yield break;
            _arrived = false;
            _moveDist = dist;
            _moveTarget = target;
            _stillSince = Time.time;
            _lastMovePos = transform.position;
            float best = Vector3.Distance(Flat(transform.position), Flat(target));
            float lastProgress = Time.time;
            float giveUp = Time.time + 20f + best * 1.5f;

            while (!_arrived && Time.time - lastProgress < StuckSeconds && Time.time < giveUp)
            {
                float now = Vector3.Distance(Flat(transform.position), Flat(target));
                if (now < best - 0.5f) { best = now; lastProgress = Time.time; }
                yield return null;
            }

            if (!_arrived)
            {
                Teleport(FreeSpotNear(target, Mathf.Max(dist * 0.8f, 1f)));
                _moveTarget = null;
                _ai.StopMoving();
            }
        }

        private void Teleport(Vector3 pos)
        {
            if (Heightmap.GetHeight(pos, out float ground) && pos.y < ground) pos.y = ground + 0.2f;
            var body = GetComponent<Rigidbody>();
            if (body) { body.linearVelocity = Vector3.zero; body.position = pos; }
            transform.position = pos;
            if (_nview && _nview.IsValid() && _nview.IsOwner()) _nview.GetZDO().SetPosition(pos);
        }

        private static int _solidMask = -1;
        private static int SolidMask => _solidMask >= 0 ? _solidMask
            : (_solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle"));

        /// <summary>A spot near a point where he can stand: on the ground, nothing solid where his body would be.</summary>
        private Vector3 FreeSpotNear(Vector3 target, float radius)
        {
            var from = Flat(transform.position - target).normalized;
            if (from.sqrMagnitude < 0.01f) from = Vector3.forward;
            Vector3? best = null;
            float bestScore = float.MaxValue;
            foreach (var r in new[] { radius, radius + 1f, radius + 2f, radius + 3.5f })
                for (int a = 0; a < 360; a += 30)
                {
                    var p = target + Quaternion.Euler(0f, a, 0f) * from * r;
                    if (!Heightmap.GetHeight(p, out float ground)) continue;
                    p.y = ground + 0.05f;
                    if (Mathf.Abs(ground - target.y) > 4f) continue;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.5f, p + Vector3.up * 1.6f, 0.35f, SolidMask)) continue;
                    float score = a * 0.01f + r + Mathf.Abs(ground - target.y);
                    if (score < bestScore) { bestScore = score; best = p; }
                }
            if (best.HasValue) return best.Value;
            var hop = target + from * radius;
            if (Heightmap.GetHeight(hop, out float h)) hop.y = Mathf.Max(h, target.y);
            return hop;
        }

        private float _nextJumpCheck, _stillSince, _lastJumpLine = -999f;
        private Vector3 _lastMovePos;

        /// <summary>Jump over anything knee-high in his way, or if he's been trying to move but hasn't for a moment.</summary>
        private void JumpIfBlocked()
        {
            if (Time.time < _nextJumpCheck || !_character) return;
            _nextJumpCheck = Time.time + 0.25f;
            if (Vector3.Distance(Flat(transform.position), Flat(_lastMovePos)) > 0.3f)
            {
                _lastMovePos = transform.position;
                _stillSince = Time.time;
            }
            if (!_character.IsOnGround()) return;
            var fwd = transform.forward;
            bool obstacle = Physics.Raycast(transform.position + Vector3.up * 0.4f, fwd, 1.3f, SolidMask)
                            && !Physics.Raycast(transform.position + Vector3.up * 1.7f, fwd, 1.6f, SolidMask);
            bool snagged = Time.time - _stillSince > 1.2f;
            if (!obstacle && !snagged) return;
            _character.Jump(true);
            _stillSince = Time.time;
            _nextJumpCheck = Time.time + 1f;
            // Only now and then: he jumps a lot, and not every hop needs a speech.
            if (_carrying && Time.time - _lastJumpLine > 40f && Random.value < 0.5f)
            {
                _lastJumpLine = Time.time;
                Say(Pick(Lines.Jump));
            }
        }

        // ---------------- talking ----------------

        private float _nextGrumble;

        private void Grumble(params string[][] sets)
        {
            _nextGrumble = Time.time + Random.Range(25f, 40f);
            if (Plugin.Chatty.Value) Say(Pick(sets));
        }

        private string Pick(params string[][] sets)
        {
            var all = sets.SelectMany(s => s).ToArray();
            return all[Random.Range(0, all.Length)].Replace("{name}", Plugin.PorterName.Value);
        }

        private void Say(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Chat.instance) Chat.instance.SetNpcText(gameObject, Vector3.up * 2.2f, 30f, 10f, "", text, false);
            Plugin.ShowMessage($"{Plugin.PorterName.Value}: {text}");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
