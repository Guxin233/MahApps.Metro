// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MahApps.Metro.Controls
{
    /// <summary>
    /// enumeration for the different transition types
    /// </summary>
    public enum TransitionType
    {
        /// <summary>
        /// DefaultTransition
        /// </summary>
        Default,
        /// <summary>
        /// Normal
        /// </summary>
        Normal,
        /// <summary>
        /// UpTransition
        /// </summary>
        Up,
        /// <summary>
        /// DownTransition
        /// </summary>
        Down,
        /// <summary>
        /// RightTransition
        /// </summary>
        Right,
        /// <summary>
        /// RightReplaceTransition
        /// </summary>
        RightReplace,
        /// <summary>
        /// LeftTransition
        /// </summary>
        Left,
        /// <summary>
        /// LeftReplaceTransition
        /// </summary>
        LeftReplace,
        /// <summary>
        /// CustomTransition
        /// </summary>
        Custom
    }

    /// <summary>
    /// A ContentControl that animates content as it loads and unloads.
    /// </summary>
    public class TransitioningContentControl : ContentControl
    {
        internal const string PresentationGroup = "PresentationStates";
        internal const string NormalState = "Normal";
        internal const string PreviousContentPresentationSitePartName = "PreviousContentPresentationSite";
        internal const string CurrentContentPresentationSitePartName = "CurrentContentPresentationSite";
        private ContentPresenter CurrentContentPresentationSite { get; set; }
        private ContentPresenter PreviousContentPresentationSite { get; set; }
        private bool _allowIsTransitioningWrite;
        private Storyboard _currentTransition;

        public event RoutedEventHandler TransitionCompleted;
        public const TransitionType DefaultTransitionState = TransitionType.Default;
       
        public static readonly DependencyProperty IsTransitioningProperty = DependencyProperty.Register("IsTransitioning", typeof(bool), typeof(TransitioningContentControl), new PropertyMetadata(OnIsTransitioningPropertyChanged));
        public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register("Transition", typeof(TransitionType), typeof(TransitioningContentControl), new FrameworkPropertyMetadata(TransitionType.Default, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.Inherits, OnTransitionPropertyChanged));
        public static readonly DependencyProperty RestartTransitionOnContentChangeProperty = DependencyProperty.Register("RestartTransitionOnContentChange", typeof(bool), typeof(TransitioningContentControl), new PropertyMetadata(false, OnRestartTransitionOnContentChangePropertyChanged));
        public static readonly DependencyProperty CustomVisualStatesProperty = DependencyProperty.Register("CustomVisualStates", typeof(ObservableCollection<VisualState>), typeof(TransitioningContentControl), new PropertyMetadata(null));

        public ObservableCollection<VisualState> CustomVisualStates {
          get { return (ObservableCollection<VisualState>)this.GetValue(CustomVisualStatesProperty); }
            set { this.SetValue(CustomVisualStatesProperty, value); }
        }

        /// <summary>
        /// Gets/sets if the content is transitioning.
        /// </summary>
        public bool IsTransitioning
        {
            get { return (bool)GetValue(IsTransitioningProperty); }
            private set
            {
                _allowIsTransitioningWrite = true;
                SetValue(IsTransitioningProperty, value);
                _allowIsTransitioningWrite = false;
            }
        }

        public TransitionType Transition
        {
            get { return (TransitionType)GetValue(TransitionProperty); }
            set { SetValue(TransitionProperty, value); }
        }

        public bool RestartTransitionOnContentChange
        {
            get { return (bool)GetValue(RestartTransitionOnContentChangeProperty); }
            set { SetValue(RestartTransitionOnContentChangeProperty, value); }
        }

        private static void OnIsTransitioningPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (TransitioningContentControl)d;

            if (!source._allowIsTransitioningWrite)
            {
                source.IsTransitioning = (bool)e.OldValue;
                throw new InvalidOperationException();
            }
        }

        private Storyboard CurrentTransition
        {
            get { return _currentTransition; }
            set
            {
                // decouple event
                if (_currentTransition != null)
                {
                    _currentTransition.Completed -= OnTransitionCompleted;
                }

                _currentTransition = value;

                if (_currentTransition != null)
                {
                    _currentTransition.Completed += OnTransitionCompleted;
                }
            }
        }

        private static void OnTransitionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var source = (TransitioningContentControl)d;
            var oldTransition = (TransitionType)e.OldValue;
            var newTransition = (TransitionType)e.NewValue;

            if (source.IsTransitioning)
            {
                source.AbortTransition();
            }

            // find new transition
            Storyboard newStoryboard = source.GetStoryboard(newTransition);

            // unable to find the transition.
            if (newStoryboard == null)
            {
                // could be during initialization of xaml that presentationgroups was not yet defined
                if (VisualStates.TryGetVisualStateGroup(source, PresentationGroup) == null)
                {
                    // will delay check
                    source.CurrentTransition = null;
                }
                else
                {
                    // revert to old value
                    source.SetValue(TransitionProperty, oldTransition);

                    throw new ArgumentException(
                        string.Format(CultureInfo.CurrentCulture, "Temporary removed exception message", newTransition));
                }
            }
            else
            {
                source.CurrentTransition = newStoryboard;
            }
        }

        private static void OnRestartTransitionOnContentChangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TransitioningContentControl)d).OnRestartTransitionOnContentChangeChanged((bool)e.OldValue, (bool)e.NewValue);
        }

        protected virtual void OnRestartTransitionOnContentChangeChanged(bool oldValue, bool newValue)
        {
        }

        public TransitioningContentControl()
        {
            this.CustomVisualStates = new ObservableCollection<VisualState>();
            DefaultStyleKey = typeof(TransitioningContentControl);
        }

        public override void OnApplyTemplate()
        {
            if (IsTransitioning)
            {
                AbortTransition();
            }

            if (this.CustomVisualStates != null && this.CustomVisualStates.Any()) {
                var presentationGroup = VisualStates.TryGetVisualStateGroup(this, PresentationGroup);
                if (presentationGroup != null) {
                    foreach (var state in this.CustomVisualStates) {
                        presentationGroup.States.Add(state);
                    }
                }
            }

            base.OnApplyTemplate();

            PreviousContentPresentationSite = GetTemplateChild(PreviousContentPresentationSitePartName) as ContentPresenter;
            CurrentContentPresentationSite = GetTemplateChild(CurrentContentPresentationSitePartName) as ContentPresenter;

            if (CurrentContentPresentationSite != null)
            {
                CurrentContentPresentationSite.Content = Content;
            }

            // hookup currenttransition
            Storyboard transition = GetStoryboard(Transition);
            CurrentTransition = transition;
            if (transition == null)
            {
                var invalidTransition = Transition;
                // revert to default
                Transition = DefaultTransitionState;

                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Temporary removed exception message", invalidTransition));
            }
            VisualStateManager.GoToState(this, NormalState, false);
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            StartTransition(oldContent, newContent);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "newContent", Justification = "Should be used in the future.")]
        private void StartTransition(object oldContent, object newContent)
        {
            // both presenters must be available, otherwise a transition is useless.
            if (CurrentContentPresentationSite != null && PreviousContentPresentationSite != null)
            {
                if (RestartTransitionOnContentChange) {
                    CurrentTransition.Completed -= OnTransitionCompleted;
                }

                CurrentContentPresentationSite.Content = newContent;

                PreviousContentPresentationSite.Content = oldContent;

                // and start a new transition
                if (!IsTransitioning || RestartTransitionOnContentChange)
                {
                    if (RestartTransitionOnContentChange) {
                        CurrentTransition.Completed += OnTransitionCompleted;
                    }
                    IsTransitioning = true;
                    VisualStateManager.GoToState(this, NormalState, false);
                    VisualStateManager.GoToState(this, GetTransitionName(Transition), true);
                }
            }
        }

        private void OnTransitionCompleted(object sender, EventArgs e)
        {
            AbortTransition();

            RoutedEventHandler handler = TransitionCompleted;
            if (handler != null)
            {
                handler(this, new RoutedEventArgs());
            }
        }

        public void AbortTransition()
        {
            // go to normal state and release our hold on the old content.
            VisualStateManager.GoToState(this, NormalState, false);
            IsTransitioning = false;
            if (PreviousContentPresentationSite != null)
            {
                PreviousContentPresentationSite.Content = null;
            }
        }

        private Storyboard GetStoryboard(TransitionType newTransition)
        {
            VisualStateGroup presentationGroup = VisualStates.TryGetVisualStateGroup(this, PresentationGroup);
            Storyboard newStoryboard = null;
            if (presentationGroup != null)
            {
                var transitionName = GetTransitionName(newTransition);
                newStoryboard = presentationGroup.States
                    .OfType<VisualState>()
                    .Where(state => state.Name == transitionName)
                    .Select(state => state.Storyboard)
                    .FirstOrDefault();
            }
            return newStoryboard;
        }

        private string GetTransitionName(TransitionType transition)
        {
            switch (transition) {
                default:
                case TransitionType.Default:
                    return "DefaultTransition";
                case TransitionType.Normal:
                    return "Normal";
                case TransitionType.Up:
                    return "UpTransition";
                case TransitionType.Down:
                    return "DownTransition";
                case TransitionType.Right:
                    return "RightTransition";
                case TransitionType.RightReplace:
                    return "RightReplaceTransition";
                case TransitionType.Left:
                    return "LeftTransition";
                case TransitionType.LeftReplace:
                    return "LeftReplaceTransition";
                case TransitionType.Custom:
                    return "CustomTransition";
            }
        }
    }
}