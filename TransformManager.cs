using System;
using System.Collections.Generic;
using System.Linq;

namespace f3
{

    public interface ITransformGizmo : SceneUIElement
    {
        List<SceneObject> Targets { get; }

        bool SupportsFrameMode { get; }
        FrameType CurrentFrameMode { get; set; }

        bool SupportsReferenceObject { get; }
        void SetReferenceObject(SceneObject so);
    }

    public interface ITransformGizmoBuilder
    {
        bool SupportsMultipleObjects { get; }
        ITransformGizmo Build(FScene scene, List<SceneObject> targets);
    }



    /// <summary>
    /// TransformManager handles Gizmos, like 3-axis transform gizmo, resizing handles, etc.
    /// Basically these are 3D widgets that appear on selection, rather than
    /// on starting a tool. For each Gizmo, a Builder is associated with a string, and then
    /// the current default Gizmo can be selected via string.
    /// 
    /// When you would like to modify or disable the default Gizmos, eg inside a Tool, you can 
    /// use  PushOverrideGizmoType/Pop. TransformManager.NoGizmoType is a built-in gizmo type
    /// that does nothing, for this purpose.
    /// 
    /// To customize which gizmo appears for a specific single object, pass GizmoTypeFilter 
    /// objects to AddTypeFilter(). To customize which gizmo appears for specific objects, pass GroupTypeFilters 
    /// objects to AddGroupTypeFilter(). Note that the current Override gizmo type still takes 
    /// precedence over the filtered gizmo type.
    /// 
    /// To just limit which SO's can be considered for a gizmo, use SetSelectionFilter()
    /// </summary>
    public class TransformManager
    {
        public FContext Context { get; set; }

        public const string NoGizmoType = "no_gizmo";
        public const string DefaultGizmoType = "default";
        
        private ITransformGizmoBuilder _activeBuilder;
        private ITransformGizmo _activeGizmo;

        private readonly Dictionary<string, ITransformGizmoBuilder> _gizmoTypes;
        private string _sActiveGizmoType;

        private string _sOverrideGizmoType;
        private readonly List<string> _overrideGizmoStack = new List<string>();
        
        private readonly List<GizmoTypeFilter> _objectFilters;
        private readonly List<GroupTypeFilter> _groupTypeFilters;

        private Func<SceneObject, bool> _selectionFilterF = null;

        private FrameType _defaultFrameType;
        private readonly Dictionary<SceneObject, FrameType> _lastFrameTypeCache;

        public TransformManager(ITransformGizmoBuilder defaultBuilder)
        {
            _activeBuilder = defaultBuilder;
            _activeGizmo = null;

            _gizmoTypes = new Dictionary<string, ITransformGizmoBuilder>();
            RegisterGizmoType(NoGizmoType, new NoGizmoBuilder());
            RegisterGizmoType(DefaultGizmoType, _activeBuilder);
            _sActiveGizmoType = DefaultGizmoType;

            _defaultFrameType = FrameType.LocalFrame;
            _lastFrameTypeCache = new Dictionary<SceneObject, FrameType>();

            _objectFilters = new List<GizmoTypeFilter>();
            _groupTypeFilters = new List<GroupTypeFilter>();

            _sOverrideGizmoType = "";
        }

        public void Initialize(FContext manager)
        {
            Context = manager;
            Context.Scene.SelectionChangedEvent += Scene_SelectionChangedEvent;
        }

        /// <summary>
        /// Associate a new gizmo builder with an identifier
        /// </summary>
        public void RegisterGizmoType(string sType, ITransformGizmoBuilder builder)
        {
            if (_gizmoTypes.ContainsKey(sType))
            {
                throw new ArgumentException($"TransformManager.RegisterGizmoType : type {sType} already registered!");
            }
            _gizmoTypes[sType] = builder;
        }

        /// <summary>
        /// Current active default gizmo type/builder
        /// </summary>
        public string ActiveGizmoType => _sActiveGizmoType;

        /// <summary>
        /// Select the current default gizmo type, using identifier passed to RegisterGizmoType()
        /// </summary>
        public void SetActiveGizmoType(string sType)
        {
            if (_sActiveGizmoType == sType)
                return;
            if (_gizmoTypes.ContainsKey(sType) == false)
                throw new ArgumentException("TransformManager.SetActiveGizmoType : type " + sType + " is not registered!");

            _activeBuilder = _gizmoTypes[sType];
            _sActiveGizmoType = sType;

            UpdateGizmo();
        }


        /// <summary>
        /// Temporarily override the current active gizmo type
        /// </summary>
        public void PushOverrideGizmoType(string sType)
        {
            if (_gizmoTypes.ContainsKey(sType) == false)
                throw new ArgumentException("TransformManager.SetOverrideGizmoType : type " + sType + " is not registered!");
            if (_overrideGizmoStack.Count > 10)
                throw new Exception("TransformManager.PushOverrideGizmoType: stack is too large, probably a missing pop?");

            _overrideGizmoStack.Add(this._sOverrideGizmoType);
            this._sOverrideGizmoType = sType;

            //update_gizmo();
            Context.RegisterNextFrameAction(UpdateGizmo);
        }

        /// <summary>
        /// Pop the override gizmo type stack
        /// </summary>
        public void PopOverrideGizmoType()
        {
            if (_overrideGizmoStack.Count == 0)
                throw new Exception("TransformManager.PopOverrideGizmoType: tried to pop empty stack!");

            this._sOverrideGizmoType = _overrideGizmoStack[_overrideGizmoStack.Count - 1];
            _overrideGizmoStack.RemoveAt(_overrideGizmoStack.Count - 1);

            // [RMS] defer this update to next frame, as we often do this inside a Tool
            //  and we should not immediately initialize gizmo...
            //update_gizmo();
            Context.RegisterNextFrameAction(UpdateGizmo);
        }

        /// <summary>
        /// Pop all pushed override gizmo types
        /// </summary>
        public void PopAllOverrideGizmos()
        {
            while (_overrideGizmoStack.Count > 0) {
                _sOverrideGizmoType = _overrideGizmoStack[_overrideGizmoStack.Count - 1];
                _overrideGizmoStack.RemoveAt(_overrideGizmoStack.Count - 1);
            }
            Context.RegisterNextFrameAction(UpdateGizmo);
        }

        /// <summary>
        /// When the selection filter is set, only objects where filterF(so) == true
        /// will be given the current gizmo.
        /// </summary>
        public void SetSelectionFilter(Func<SceneObject, bool> filterF)
        {
            _selectionFilterF = filterF;
        }

        /// <summary>
        /// Discard current selection filter
        /// </summary>
        public void ClearSelectionFilter() => _selectionFilterF = null;

        /// <summary>
        /// TypeFilters will checked each time the selection changes. If the filter returns
        /// a gizmo type identifier, we'll use that instead of the current default.
        /// However override gizmos will still take precedence.
        /// </summary>
        public void AddTypeFilter(GizmoTypeFilter filter) => _objectFilters.Add(filter);

        /// <summary>
        /// remove a previously-registered gizmo type filter
        /// </summary>
        public void RemoveTypeFilter(GizmoTypeFilter filter) => _objectFilters.Remove(filter);
        
        /// <summary>
        /// remove all registered gizmo type filters
        /// </summary>
        public void ClearAllTypeFilters() => _objectFilters.Clear();
        
        /// <summary>
        /// GroupTypeFilters will checked each time the selection changes. If the filter returns
        /// a gizmo type identifier, we'll use that instead of the current default.
        /// However override gizmos will still take precedence.
        /// </summary>
        public void AddGroupTypeFilter(GroupTypeFilter groupTypeFilter) => _groupTypeFilters.Add(groupTypeFilter);

        /// <summary>
        ///  remove a previously-registered group type filter
        /// </summary>
        public void RemoveGroupTypeFilter(GroupTypeFilter groupTypeFilter) => _groupTypeFilters.Remove(groupTypeFilter);

        /// <summary>
        /// remove all registered group type filters
        /// </summary>
        public void ClearAllGroupTypeFilters() => _groupTypeFilters.Clear();

        public bool HaveActiveGizmo => _activeGizmo != null;

        public ITransformGizmo ActiveGizmo => _activeGizmo;

        public event EventHandler OnActiveGizmoModified;

        protected virtual void SendOnActiveGizmoModified()
        {
            EventHandler tmp = OnActiveGizmoModified;
            tmp?.Invoke(this, new EventArgs());
        }

        public FrameType ActiveFrameType
        {
            get
            {
                return _activeGizmo != null && _activeGizmo.SupportsFrameMode
                    ? _activeGizmo.CurrentFrameMode
                    : _defaultFrameType;
            }
            set
            {
                if (_activeGizmo != null && _activeGizmo.SupportsFrameMode)
                {
                    _activeGizmo.CurrentFrameMode = value;
                    if (_activeGizmo.Targets.Count == 1)
                    {
                        _lastFrameTypeCache[_activeGizmo.Targets[0]] = value;
                    }
                    //defaultFrameType = value;       // always change default when we explicitly change type
                    SendOnActiveGizmoModified();
                }
                else
                {
                    _defaultFrameType = value;
                    SendOnActiveGizmoModified(); // not really right...but UI using it now
                }
            }
        }

        public void SetActiveReferenceObject(SceneObject so)
        {
            if (_activeGizmo != null && _activeGizmo.SupportsReferenceObject)
            {
                _activeGizmo.SetReferenceObject(so);
            }
        }

        public void DismissActiveGizmo()
        {
            FScene scene = Context.Scene;
            if (_activeGizmo != null)
            {
                scene.RemoveUIElement(_activeGizmo, true);
                _activeGizmo = null;
                SendOnActiveGizmoModified();
            }
        }

        private static FrameType InitialFrameType(SceneObject so) => FrameType.LocalFrame;

        protected void AddGizmo(List<SceneObject> targets)
        {
            ITransformGizmoBuilder gizmoBuilderToUse = ChooseGizmoBuilders(targets, defaultBuilder: _activeBuilder);

            // filter target count if builder only supports single object
            List<SceneObject> useTargets = new List<SceneObject>(targets);
            
            if (useTargets.Count > 0 && gizmoBuilderToUse.SupportsMultipleObjects == false)
            {
                useTargets.RemoveRange(1, useTargets.Count - 1);
            }

            // remove existing active gizmo
            // [TODO] support multiple gizmos?
            if (_activeGizmo != null)
            {
                if (UnorderedListsEqual(_activeGizmo.Targets, useTargets))
                {
                    return; // same targets
                }

                DismissActiveGizmo();
            }

            if (targets != null)
            {
                _activeGizmo = gizmoBuilderToUse.Build(Context.Scene, useTargets);

                if (_activeGizmo == null)
                {
                    return;
                }

                // set frame type. behavior here is a bit tricky...we have a default frame type
                // and then a cached type for each object. However if we only cache type on explicit
                // user changes, then if user changes default, all other gizmos inherit this default.
                // This is currently a problem because we are also using default frame type to
                // control things like snapping behavior (local=translate+rotate, world=translate-only).
                // So then if we change that, we can change default, which then changes object gizmo 
                // behavior in unexpected ways. So right now we are initializing cache with a per-type
                // default (always Local right now), which user can then change. This "feels" right-est...
                if (_activeGizmo.SupportsFrameMode)
                {
                    if (targets.Count == 1)
                    {
                        if (_lastFrameTypeCache.ContainsKey(useTargets[0]) == false)
                        {
                            _lastFrameTypeCache[useTargets[0]] = InitialFrameType(useTargets[0]);
                        }

                        _activeGizmo.CurrentFrameMode = _lastFrameTypeCache[useTargets[0]];
                    }
                    else
                    {
                        _activeGizmo.CurrentFrameMode = _defaultFrameType;
                    }
                }

                Context.Scene.AddUIElement(_activeGizmo);
                SendOnActiveGizmoModified();
            }
        }

        private ITransformGizmoBuilder ChooseGizmoBuilders(List<SceneObject> targets,
            ITransformGizmoBuilder defaultBuilder)
        {
            // current default active gizmo builder
            // use override if defined
            if (!string.IsNullOrEmpty(_sOverrideGizmoType))
            {
                return _gizmoTypes[_sOverrideGizmoType];
            }

            if (targets.Count >= 1 && _groupTypeFilters.Count > 0)
            {
                foreach (GroupTypeFilter filter in _groupTypeFilters)
                {
                    string typeName = filter.FilterF(targets);
                    if (typeName != null && _gizmoTypes.ContainsKey(typeName))
                    {
                        return _gizmoTypes[typeName];
                    }
                }
            }

            if (targets.Count == 1 && _objectFilters.Count > 0)
            {
                foreach (GizmoTypeFilter filter in _objectFilters)
                {
                    string typeName = filter.FilterF(targets[0]);
                    if (typeName != null && _gizmoTypes.ContainsKey(typeName))
                    {
                        return _gizmoTypes[typeName];
                    }
                }
            }

            return defaultBuilder;
        }

        private static bool UnorderedListsEqual<T>(IReadOnlyCollection<T> l1, ICollection<T> l2) => 
            l1.Count == l2.Count && l1.All(l2.Contains);

        private void Scene_SelectionChangedEvent(object sender, EventArgs e)
        {
            FScene scene = Context.Scene;
            List<SceneObject> vSelected = new List<SceneObject>();
            foreach (SceneObject tso in scene.Selected)
            {
                if (tso != null && (_selectionFilterF == null || _selectionFilterF(tso)))
                {
                    vSelected.Add(tso);
                }
            }

            if (vSelected.Count == 0 && _activeGizmo != null)
            {
                // object de-selected, dismiss gizmo
                DismissActiveGizmo();
                return;
            }

            if (_activeGizmo != null && UnorderedListsEqual(vSelected, _activeGizmo.Targets) == false)
            {
                DismissActiveGizmo();
            }

            if (vSelected.Count > 0)
            {
                Context.RegisterNextFrameAction(AddGizmoNextFrame);
            }
            //AddGizmo(vSelected);
        }

        private void AddGizmoNextFrame()
        {
            FScene scene = Context.Scene;
            List<SceneObject> vSelected = new List<SceneObject>();
            foreach (SceneObject tso in scene.Selected)
            {
                if (tso != null && (_selectionFilterF == null || _selectionFilterF(tso)))
                {
                    vSelected.Add(tso);
                }
            }

            if (vSelected.Count > 0)
            {
                AddGizmo(vSelected);
            }
        }

        private void UpdateGizmo()
        {
            DismissActiveGizmo();
            Scene_SelectionChangedEvent(null, null);
        }
        
        /// <summary>
        /// Used to replace default gizmo type - see AddTypeFilter()
        /// </summary>
        public class GizmoTypeFilter
        {
            /// <summary> return Gizmo type name string, or null </summary>
            public Func<SceneObject, string> FilterF;
        }
        
        /// <summary>
        /// Used to replace multiple gizmo type - see AddGroupTypeFilter()
        /// </summary>
        public class GroupTypeFilter
        {
            public Func<IReadOnlyList<SceneObject>, string> FilterF { get; set; }
        }
    }

    public class NoGizmoBuilder : ITransformGizmoBuilder
    {
        public bool SupportsMultipleObjects {
            get { return true; }
        }
        public ITransformGizmo Build(FScene scene, List<SceneObject> targets)
        {
            return null;
        }
    }

}
