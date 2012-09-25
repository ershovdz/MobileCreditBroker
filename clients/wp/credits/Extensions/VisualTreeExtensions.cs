using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows.Media;
using System.Windows;
using System.Collections;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Credits.Extensions
{
    public static class VisualTreeExtensions
    {
        public static IEnumerable<DependencyObject> GetVisualAncestors(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualAncestorsAndSelfIterator(element).Skip(1);
        }

        public static IEnumerable<DependencyObject> GetVisualAncestorsAndSelf(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualAncestorsAndSelfIterator(element);
        }

        private static IEnumerable<DependencyObject> GetVisualAncestorsAndSelfIterator(DependencyObject element)
        {
            Debug.Assert(element != null, "element should not be null!");

            for (DependencyObject obj = element;
                    obj != null;
                    obj = VisualTreeHelper.GetParent(obj))
            {
                yield return obj;
            }
        }

        public static IEnumerable<T> GetVisualChildren<T>(this DependencyObject target)
            where T : DependencyObject
        {
            return GetVisualChildren(target).Where(child => child is T).Cast<T>();
        }

        public static IEnumerable<T> GetVisualChildren<T>(this DependencyObject target, bool strict)
            where T : DependencyObject
        {
            return GetVisualChildren(target, strict).Where(child => child is T).Cast<T>();
        }

        public static IEnumerable<DependencyObject> GetVisualChildren(this DependencyObject target, bool strict)
        {
            int count = VisualTreeHelper.GetChildrenCount(target);
            if (count == 0)
            {
                if (!strict && target is ContentControl)
                {
                    var child = ((ContentControl)target).Content as DependencyObject;
                    if (child != null)
                    {
                        yield return child;
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    yield return VisualTreeHelper.GetChild(target, i);
                }
            }
            yield break;
        }

        public static IEnumerable<DependencyObject> GetVisualChildren(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualChildrenAndSelfIterator(element).Skip(1);
        }

        public static IEnumerable<DependencyObject> GetVisualChildrenAndSelf(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualChildrenAndSelfIterator(element);
        }

        private static IEnumerable<DependencyObject> GetVisualChildrenAndSelfIterator(this DependencyObject element)
        {
            Debug.Assert(element != null, "element should not be null!");

            yield return element;

            int count = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < count; i++)
            {
                yield return VisualTreeHelper.GetChild(element, i);
            }
        }

        private static IEnumerable<DependencyObject> GetVisualDecendants(DependencyObject target, bool strict, Queue<DependencyObject> queue)
        {
            foreach (var child in GetVisualChildren(target, strict))
            {
                queue.Enqueue(child);
            }

            if (queue.Count == 0)
            {
                yield break;
            }
            else
            {
                DependencyObject node = queue.Dequeue();
                yield return node;

                foreach (var decendant in GetVisualDecendants(node, strict, queue))
                {
                    yield return decendant;
                }
            }
        }

        private static IEnumerable<DependencyObject> GetVisualDecendants(DependencyObject target, bool strict, Stack<DependencyObject> stack)
        {
            foreach (var child in GetVisualChildren(target, strict))
            {
                stack.Push(child);
            }

            if (stack.Count == 0)
            {
                yield break;
            }
            else
            {
                DependencyObject node = stack.Pop();
                yield return node;

                foreach (var decendant in GetVisualDecendants(node, strict, stack))
                {
                    yield return decendant;
                }
            }
        }

        public static IEnumerable<DependencyObject> GetVisualDescendants(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualDescendantsAndSelfIterator(element).Skip(1);
        }

        public static IEnumerable<DependencyObject> GetVisualDescendantsAndSelf(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            return GetVisualDescendantsAndSelfIterator(element);
        }

        private static IEnumerable<DependencyObject> GetVisualDescendantsAndSelfIterator(DependencyObject element)
        {
            Debug.Assert(element != null, "element should not be null!");

            Queue<DependencyObject> remaining = new Queue<DependencyObject>();
            remaining.Enqueue(element);

            while (remaining.Count > 0)
            {
                DependencyObject obj = remaining.Dequeue();
                yield return obj;

                foreach (DependencyObject child in obj.GetVisualChildren())
                {
                    remaining.Enqueue(child);
                }
            }
        }

        public static IEnumerable<DependencyObject> GetVisualSiblings(this DependencyObject element)
        {
            return element
                .GetVisualSiblingsAndSelf()
                .Where(p => p != element);
        }

        public static IEnumerable<DependencyObject> GetVisualSiblingsAndSelf(this DependencyObject element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            DependencyObject parent = VisualTreeHelper.GetParent(element);
            return parent == null ?
                Enumerable.Empty<DependencyObject>() :
                parent.GetVisualChildren();
        }

        public static Rect? GetBoundsRelativeTo(this FrameworkElement element, UIElement otherElement)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            else if (otherElement == null)
            {
                throw new ArgumentNullException("otherElement");
            }

            try
            {
                Point origin, bottom;
                GeneralTransform transform = element.TransformToVisual(otherElement);
                if (transform != null &&
                    transform.TryTransform(new Point(), out origin) &&
                    transform.TryTransform(new Point(element.ActualWidth, element.ActualHeight), out bottom))
                {
                    return new Rect(origin, bottom);
                }
            }
            catch (ArgumentException)
            {
                // Ignore any exceptions thrown while trying to transform
            }

            return null;
        }

        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                {
                    return (childItem)child;
                }
                else
                {
                    var childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        public static FrameworkElement FindVisualChild(this FrameworkElement root, string name)
        {
            FrameworkElement temp = root.FindName(name) as FrameworkElement;
            if (temp != null)
                return temp;

            foreach (FrameworkElement element in root.GetVisualChildren())
            {
                temp = element.FindName(name) as FrameworkElement;
                if (temp != null)
                    return temp;
            }

            return null;
        }

        public static IEnumerable<DependencyObject> GetVisuals(this DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var descendants in child.GetVisuals())
                {
                    yield return descendants;
                }
            }
        }

        public static FrameworkElement GetVisualChild(this FrameworkElement node, int index)
        {
            return VisualTreeHelper.GetChild(node, index) as FrameworkElement;
        }

        public static FrameworkElement GetVisualParent(this FrameworkElement node)
        {
            return VisualTreeHelper.GetParent(node) as FrameworkElement;
        }

        public static VisualStateGroup GetVisualStateGroup(this FrameworkElement root, string groupName, bool searchAncestors)
        {
            IList groups = VisualStateManager.GetVisualStateGroups(root);
            foreach (object o in groups)
            {
                VisualStateGroup group = o as VisualStateGroup;
                if (group != null && group.Name == groupName)
                    return group;
            }

            if (searchAncestors)
            {
                FrameworkElement parent = root.GetVisualParent();
                if (parent != null)
                    return parent.GetVisualStateGroup(groupName, true);
            }

            return null;
        }

        [Conditional("DEBUG")]
        public static void GetVisualChildTreeDebugText(this FrameworkElement root, StringBuilder result)
        {
            List<string> results = new List<string>();
            root.GetChildTree("", "  ", results);
            foreach (string s in results)
                result.AppendLine(s);
        }

        private static void GetChildTree(this FrameworkElement root, string prefix, string addPrefix, List<string> results)
        {
            string thisElement = "";
            if (String.IsNullOrEmpty(root.Name))
                thisElement = "[Anonymous]";
            else
                thisElement = "[" + root.Name + "]";

            thisElement += " : " + root.GetType().Name;

            results.Add(prefix + thisElement);
            foreach (FrameworkElement directChild in root.GetVisualChildren())
            {
                directChild.GetChildTree(prefix + addPrefix, addPrefix, results);
            }
        }

        [Conditional("DEBUG")]
        public static void GetAncestorVisualTreeDebugText(this FrameworkElement node, StringBuilder result)
        {
            List<string> tree = new List<string>();
            node.GetAncestorVisualTree(tree);
            string prefix = "";
            foreach (string s in tree)
            {
                result.AppendLine(prefix + s);
                prefix = prefix + "  ";
            }
        }

        private static void GetAncestorVisualTree(this FrameworkElement node, List<string> children)
        {
            string name = String.IsNullOrEmpty(node.Name) ? "[Anon]" : node.Name;
            string thisNode = name + ": " + node.GetType().Name;

            // Ensure list is in reverse order going up the tree
            children.Insert(0, thisNode);
            FrameworkElement parent = node.GetVisualParent();
            if (parent != null)
                GetAncestorVisualTree(parent, children);
        }

        public static RequestedTransform GetTransform<RequestedTransform>(this UIElement element, TransformCreationMode mode) where RequestedTransform : Transform, new()
        {
            //if (element == null)
            //    return null;

            Transform originalTransform = element.RenderTransform;
            RequestedTransform requestedTransform = null;
            MatrixTransform matrixTransform = null;
            TransformGroup transformGroup = null;

            // Current transform is null -- create if necessary and return
            if (originalTransform == null)
            {
                if ((mode & TransformCreationMode.Create) == TransformCreationMode.Create)
                {
                    requestedTransform = new RequestedTransform();
                    element.RenderTransform = requestedTransform;
                    return requestedTransform;
                }

                return null;
            }

            // Transform is exactly what we want -- return it
            requestedTransform = originalTransform as RequestedTransform;
            if (requestedTransform != null)
                return requestedTransform;


            // The existing transform is matrix transform - overwrite if necessary and return
            matrixTransform = originalTransform as MatrixTransform;
            if (matrixTransform != null)
            {
                if (matrixTransform.Matrix.IsIdentity
                  && (mode & TransformCreationMode.Create) == TransformCreationMode.Create
                  && (mode & TransformCreationMode.IgnoreIdentityMatrix) == TransformCreationMode.IgnoreIdentityMatrix)
                {
                    requestedTransform = new RequestedTransform();
                    element.RenderTransform = requestedTransform;
                    return requestedTransform;
                }

                return null;
            }

            // Transform is actually a group -- check for the requested type
            transformGroup = originalTransform as TransformGroup;
            if (transformGroup != null)
            {
                foreach (Transform child in transformGroup.Children)
                {
                    // Child is the right type -- return it
                    if (child is RequestedTransform)
                        return child as RequestedTransform;
                }

                // Right type was not found, but we are OK to add it
                if ((mode & TransformCreationMode.AddToGroup) == TransformCreationMode.AddToGroup)
                {
                    requestedTransform = new RequestedTransform();
                    transformGroup.Children.Add(requestedTransform);
                    return requestedTransform;
                }

                return null;
            }

            // Current ransform is not a group and is not what we want;
            // create a new group containing the existing transform and the new one
            if ((mode & TransformCreationMode.CombineIntoGroup) == TransformCreationMode.CombineIntoGroup)
            {
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(originalTransform);
                transformGroup.Children.Add(requestedTransform);
                element.RenderTransform = transformGroup;
                return requestedTransform;
            }

            Debug.Assert(false, "Shouldn't get here");
            return null;
        }

        public static string GetTransformPropertyPath<RequestedType>(this FrameworkElement element, string subProperty) where RequestedType : Transform
        {
            Transform t = element.RenderTransform;
            if (t is RequestedType)
                return String.Format("(RenderTransform).({0}.{1})", typeof(RequestedType).Name, subProperty);

            else if (t is TransformGroup)
            {
                TransformGroup g = t as TransformGroup;
                for (int i = 0; i < g.Children.Count; i++)
                {
                    if (g.Children[i] is RequestedType)
                        return String.Format("(RenderTransform).(TransformGroup.Children)[" + i + "].({0}.{1})",
                          typeof(RequestedType).Name, subProperty);
                }
            }

            return "";
        }

        public static PlaneProjection GetPlaneProjection(this UIElement element, bool create)
        {
            Projection originalProjection = element.Projection;
            PlaneProjection projection = null;

            // Projection is already a plane projection; return it
            if (originalProjection is PlaneProjection)
                return originalProjection as PlaneProjection;

            // Projection is null; create it if necessary
            if (originalProjection == null)
            {
                if (create)
                {
                    projection = new PlaneProjection();
                    element.Projection = projection;
                }
            }

            // Note that if the project is a Matrix projection, it will not be
            // changed and null will be returned.
            return projection;
        }

        public static void InvokeOnLayoutUpdated(this FrameworkElement element, Action action)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }
            else if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            // Create an event handler that unhooks itself before calling the
            // action and then attach it to the LayoutUpdated event.
            EventHandler handler = null;
            handler = (s, e) =>
            {
                //TODO: is this the right thing to do?
                //Deployment.Current.Dispatcher.BeginInvoke(() => { element.LayoutUpdated -= handler; });
                element.LayoutUpdated -= handler;

                action();
            };
            element.LayoutUpdated += handler;
        }
    }

    [Flags]
    public enum TransformCreationMode
    {
        /// <summary>
        /// Don't try and create a transform if it doesn't already exist
        /// </summary>
        None = 0,

        /// <summary>
        /// Create a transform if none exists
        /// </summary>
        Create = 1,

        /// <summary>
        /// Create and add to an existing group
        /// </summary>
        AddToGroup = 2,

        /// <summary>
        /// Create a group and combine with existing transform; may break existing animations
        /// </summary>
        CombineIntoGroup = 4,

        /// <summary>
        /// Treat identity matrix as if it wasn't there; may break existing animations
        /// </summary>
        IgnoreIdentityMatrix = 8,

        /// <summary>
        /// Create a new transform or add to group
        /// </summary>
        CreateOrAddAndIgnoreMatrix = Create | AddToGroup | IgnoreIdentityMatrix,

        /// <summary>
        /// Default behaviour, equivalent to CreateOrAddAndIgnoreMatrix
        /// </summary>
        Default = CreateOrAddAndIgnoreMatrix,
    }
}