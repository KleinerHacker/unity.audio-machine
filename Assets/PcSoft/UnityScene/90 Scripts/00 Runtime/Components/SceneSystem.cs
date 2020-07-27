using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PcSoft.ExtendedEditor._90_Scripts._00_Runtime.Extra;
using PcSoft.ExtendedEditor._90_Scripts._00_Runtime.Types;
using PcSoft.ExtendedEditor._90_Scripts._00_Runtime.Utils;
using PcSoft.ExtendedUnity._90_Scripts._00_Runtime.Utils.Extensions;
#if UNITY_EDITOR
using PcSoft.ExtendedUnity._90_Scripts._90_Editor.EditorEvents;
#endif
#if UNITY_EDITOR
#endif
using PcSoft.UnityBlending._90_Scripts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PcSoft.UnityScene._90_Scripts._00_Runtime.Components
{
    public abstract class SceneSystem<TScene, T> : MonoBehaviour where TScene : SceneData<T> where T : Enum
    {
        #region Inspector Data

        [SerializeField]
        protected BlendingSystem blending;

        [SerializeField]
        protected TScene[] scenes;

        [SerializeField]
        private T initialState;

        #endregion

        #region Properties

        public T State { get; protected set; }

        #endregion

        private readonly bool _firstLoadImmediately;

        protected SceneSystem(bool firstLoadImmediately = false)
        {
            scenes = ArrayUtils.CreateIdentifierArray<TScene, T>();
            _firstLoadImmediately = firstLoadImmediately;
        }

        #region Builtin Methods

        protected virtual void Awake()
        {
#if UNITY_EDITOR
            if (AutoMasterOnPlayParameter.DoNotLoadOnAwake)
            {
                blending.HideBlendImmediately();
                return;
            }

            State = initialState;
            if (_firstLoadImmediately)
            {
                LoadSceneImmediately(State);
            }
            else
            {
                LoadScene(State, doNotUnload: true);
            }
#else
            if (_firstLoadImmediately)
            {
                LoadSceneImmediately(State);
            }
            else
            {
                LoadScene(State, doNotUnload: true);
            }
#endif
        }

        #endregion

        #region Private Methods

        protected void LoadScene(T state, object data = null, Action onFinished = null, bool doNotUnload = false)
        {
            string[] oldScenes = null;
            TScene oldData = null;
            if (!doNotUnload)
            {
                oldData = FindSceneData(State, data);
                oldScenes = oldData.Scenes;
            }

            var newData = FindSceneData(state, data);
            var newScenes = newData.Scenes;
            
            OnLoadingStarted(State, oldData);

            blending.ShowBlend(() =>
            {
                StartCoroutine(ChangeScenes(oldScenes, newScenes, () =>
                {
                    blending.HideBlend(() =>
                    {
                        OnLoadingFinished(state, newData);

                        State = state;
                        onFinished?.Invoke();
                    });
                }));
            });
        }

        protected void LoadSceneImmediately(T state, object data = null)
        {
            var newData = FindSceneData(state, data);
            var newScenes = newData.Scenes;

            foreach (var newScene in newScenes)
            {
                var op = SceneManager.LoadSceneAsync(newScene, LoadSceneMode.Additive);
                op.completed += operation => SceneManager.SetActiveScene(SceneManager.GetSceneByPath(newScenes[0]));
            }
            
            OnLoadingFinished(state, newData);
        }

        protected virtual TScene FindSceneData(T state, object data)
        {
            return scenes.First(item => Equals(item.Identifier, state));
        }

        private IEnumerator ChangeScenes(string[] oldScenes, string[] newScenes, Action onFinished)
        {
            if (oldScenes != null && oldScenes.Length > 0)
            {
                foreach (var oldScene in oldScenes)
                {
                    SceneManager.UnloadSceneAsync(oldScene, UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);
                }
            }

            var operations = new List<AsyncOperation>();
            foreach (var newScene in newScenes)
            {
                var operation = SceneManager.LoadSceneAsync(newScene, LoadSceneMode.Additive);
                operation.allowSceneActivation = false;
                operation.completed += asyncOperation => SceneManager.SetActiveScene(SceneManager.GetSceneByPath(newScenes[0]));

                operations.Add(operation);
            }


            while (!operations.IsReady())
            {
                blending.LoadingProgress = operations.CalculateProgress();
                yield return null;
            }

            foreach (var operation in operations)
            {
                operation.allowSceneActivation = true;
            }

            while (!operations.IsDone())
            {
                blending.LoadingProgress = operations.CalculateProgress();
                yield return null;
            }

            onFinished?.Invoke();
        }

        #endregion

        protected virtual void OnLoadingStarted(T oldState, TScene scene)
        {
        }

        protected virtual void OnLoadingFinished(T newState, TScene scene)
        {
        }
    }

    [Serializable]
    public abstract class SceneData<T> : IIdentifiedObject<T> where T : Enum
    {
        #region Inspector Data

        [HideInInspector]
        [SerializeField]
        private T identifier;

        [Scene]
        [SerializeField]
        private string[] scenes;

        #endregion

        #region Properties

        public T Identifier => identifier;

        public virtual string[] Scenes => scenes;

        #endregion

        protected SceneData(T identifier)
        {
            this.identifier = identifier;
        }
        
        protected SceneData(T identifier, string[] scenes)
        {
            this.identifier = identifier;
            this.scenes = scenes;
        }
    }
}