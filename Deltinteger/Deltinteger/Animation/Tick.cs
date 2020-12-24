using System;
using Deltin.Deltinteger.Elements;
using Deltin.Deltinteger.Parse;
using static Deltin.Deltinteger.Animation.AnimationOperations;

namespace Deltin.Deltinteger.Animation
{
    public class AnimationTick : IComponent
    {
        public DeltinScript DeltinScript { get; set; }
        private BaseAnimationInstanceType _objectType;
        private MeshInstanceType _meshType;
        private ArmatureInstanceType _armatureType;
        private ClassData _classData;
        /// <summary>An array of object references.</summary>
        private IndexReference _animationReferenceList;
        /// <summary>[x]: the object reference in correlation to _animationReferenceList.
        /// <p>[x][0]: the action index.
        /// <p>[x][1]: the action start time.
        /// <p>[x][2]: should the animation loop?</summary>
        private IndexReference _animationInfoList;
        private ActionSet actionSet;
        private Element currentReference;
        Element BoneVertexLinks {
            get => _armatureType.BoneVertexLinks.Get(currentReference);
            set => _armatureType.BoneVertexLinks.Set(actionSet, currentReference, value);
        }
        Element BoneLocalPositions {
            get => _armatureType.BoneLocalPositions.Get(currentReference);
            set => _armatureType.BoneLocalPositions.Set(actionSet, currentReference, value);
        }
        Element BoneDescendants => _armatureType.BoneDescendants.Get(currentReference);
        Element BonePositions => _armatureType.BonePositions.Get(currentReference);
        Element BonePointParents => _armatureType.BonePointParents.Get(currentReference);
        Element BonePointsBone => _armatureType.BonePointsBone.Get(currentReference);
        private Element Actions => _armatureType.Actions.Get(currentReference);

        public AnimationTick() {}

        public void Init()
        {
            _objectType = DeltinScript.Types.GetInstance<BaseAnimationInstanceType>();
            _meshType = DeltinScript.Types.GetInstance<MeshInstanceType>();
            _armatureType = DeltinScript.Types.GetInstance<ArmatureInstanceType>();
            _classData = DeltinScript.GetComponent<ClassData>();

            _animationReferenceList = DeltinScript.VarCollection.Assign("animation_reference_list", true, false);
            _animationInfoList = DeltinScript.VarCollection.Assign("animation_info_list", true, false);
            DeltinScript.InitialGlobal.ActionSet.AddAction(_animationReferenceList.SetVariable(new V_EmptyArray()));
            DeltinScript.InitialGlobal.ActionSet.AddAction(_animationInfoList.SetVariable(new V_EmptyArray()));

            DeltinScript.WorkshopRules.Add(GetRule());
        }

        Rule GetRule()
        {
            var ruleBuilder = new TranslateRule(DeltinScript, "Animation -> Loop", RuleEvent.OngoingGlobal);

            // When _animationReferenceList.Length != 0
            ruleBuilder.Conditions.Add(new Condition(ReferenceListCount, Operators.NotEqual, 0));

            actionSet = ruleBuilder.ActionSet;
            
            // Get the actions.
            Loop();

            return ruleBuilder.GetRule();
        }

        void Loop()
        {
            var referenceLoop = new ForRangeBuilder(actionSet, "animation_ref_loop", 0, ReferenceListCount, 1);
            referenceLoop.Init();
            currentReference = actionSet.AssignAndSave("animation_current_reference", _animationReferenceList.Get()[referenceLoop.Value]).Get();

            // Armature
            actionSet.AddAction(Element.Part<A_If>(_classData.IsInstanceOf(currentReference, _armatureType)));

            var localMatrices = actionSet.AssignAndSave("local_matrices", new V_EmptyArray());

            // Loop through each bone
            var boneLoop = new ForRangeBuilder(actionSet, "animation_bone_loop", 0, Element.Part<V_CountOf>(BoneVertexLinks), 1);
            boneLoop.Init();

            // Debug bone name
            DebugVariable(actionSet, "db_current_bone", _armatureType.BoneNames.Get(currentReference)[boneLoop]);

            // Loop through each action.
            var actionLoop = new ForRangeBuilder(actionSet, "animation_action_loop", 0, Element.Part<V_CountOf>(_animationInfoList.Get()[referenceLoop.Value]), 1);
            actionLoop.Init();

            Element actionIdentifier = _animationInfoList.Get()[referenceLoop.Value][actionLoop.Value][0];
            Element currentActionTime = _animationInfoList.Get()[referenceLoop.Value][actionLoop.Value][1];
            Element currentActionShouldLoop = _animationInfoList.Get()[referenceLoop.Value][actionLoop.Value][2];

            currentActionTime = actionSet.AssignAndSave("animation_current_action_time", currentActionTime).Get();
            Element currentActionTimeDelta = actionSet.AssignAndSave("animation_delta", new V_TotalTimeElapsed() - currentActionTime).Get();

            // Get the fcurve related to this bone.
            var fcurve = actionSet.AssignAndSave("animation_curve", Element.Part<V_FilteredArray>(
                // Get the fcurve array for the current object and action.
                _objectType.Actions.Get(currentReference) // The action array
                    [ // Getting a value in the action array will return an array of fcurves.
                        // Get the action index. The action index being used is stored in the _animationInfoList array.
                        // [reference loop index][action loop index][0 = gets real action index]
                        _animationInfoList.Get()[referenceLoop.Value][actionLoop.Value][0]
                    ],
                // Get the f-curve for the bone we are currently iterating on.
                Element.Part<V_And>(Element.Part<V_And>(
                    // The first value in the action array is the frame range.
                    // Make sure this is not the first element.
                    new V_CurrentArrayIndex(),
                    // The Fcurve's type is BoneRotation.
                    new V_Compare(new V_ArrayElement()[0], Operators.Equal, (Element)(int)FCurveType.BoneRotation)),
                    // The Fcurve's bone index is equal to the target bone.
                    new V_Compare(boneLoop.Value, Operators.Equal, new V_ArrayElement()[1])
                )
            )[0]); // FilteredArray will return an array with 1 value. Use [0] to convert from '[x]' to 'x'.

            var local = actionSet.AssignAndSave("animation_matrix_local", _armatureType.BoneMatrices.Get(currentReference)[boneLoop.Value]);
            Element parentBoneIndex = actionSet.AssignAndSave("animation_bone_parent", _armatureType.BoneParents.Get(currentReference)[boneLoop.Value]).Get();

            // 'local' is an array-grouped matrix.
            // If the bone has a parent, multiply the parent bone's local matrix.
            actionSet.AddAction(Element.Part<A_If>(new V_Compare(parentBoneIndex, Operators.NotEqual, new V_Number(-1))));
                // Convert 'local' to a column grouped matrix.
                // The matrix multiplication function requires the right matrix to be column grouped.
                actionSet.AddAction(local.SetVariable(ConvertArrayGroupedMatrixToColumnGroupedMatrix(local.Get())));

                // Save the parent index.
                Element parentLocal = actionSet.AssignAndSave("animation_parent_local", localMatrices.Get()[parentBoneIndex]).Get();

                // Multiply the matrices: Set 'local' to 'parentLocal @ local'.
                // This assumes 'parentLocal' is already a row-grouped matrix.
                actionSet.AddAction(local.SetVariable(VectorNotatedMultiplyMatrix3x3AndMatrix3x3(parentLocal, local.Get())));

            // End the if.
            actionSet.AddAction(new A_End());

            // Ew yuck no curve
            actionSet.AddAction(Element.Part<A_If>(new V_Compare(fcurve.Get(), Operators.Equal, new V_Number(0))));
                // Save the matrix.
                actionSet.AddAction(localMatrices.SetVariable(ConvertArrayGroupedMatrixToRowGroupedMatrix(local.Get()), index: boneLoop.Value));

            actionSet.AddAction(new A_Else());

            Element fps = 24;

            // Now we get the A and B keyframes from the fcurve using the current time in the animation.
            // This will occur if all keyframes were surpassed.
            var keyframe_index = actionSet.AssignAndSave("animation_keyframe_index", Element.Part<V_FirstOf>(Element.Part<V_FilteredArray>(
                // We want the index of the keyframe, convert to a range of numbers.
                Element.Part<V_MappedArray>(fcurve.Get(), new V_CurrentArrayIndex()),
                Element.Part<V_And>(
                    // Ignore the first 2 elements, which is fcurve data.
                    new V_Compare(new V_ArrayElement(), Operators.GreaterThanOrEqual, new V_Number(2)),
                    new V_Compare(
                        currentActionTimeDelta * fps % Element.TernaryConditional(currentActionShouldLoop, Actions[actionIdentifier][0], V_Number.LargeArbitraryNumber),
                        Operators.LessThan,
                        fcurve.Get()[new V_ArrayElement()][0]
                    )
                )
            )));

            actionSet.AddAction(Element.Part<A_If>(Element.Part<V_Not>(keyframe_index.Get())));
            actionSet.AddAction(keyframe_index.SetVariable(Element.Part<V_CountOf>(fcurve.Get()) - 1));
            actionSet.AddAction(new A_End());

            // Keyframe A is fcurve[i - 1], keyframe B is furve[i].
            var keyframeA = actionSet.AssignAndSave("animation_keyframe_a", fcurve.Get()[Element.Part<V_Max>(keyframe_index.Get() - 1, (Element)2)]).Get(); // Save to variable if needed.
            var keyframeB = actionSet.AssignAndSave("animation_keyframe_b", fcurve.Get()[Element.Part<V_Max>(keyframe_index.Get())]).Get(); // Save to variable if needed.

            // Get the t of the keyframes depending on the current time.
            //  If the action was started at 5 seconds (s),
            //  and keyframe A is set at 2 (7) local seconds (a),
            //  and keyframe B is set at 6 (11) local seconds (b),
            //  and the current time is 9 (c),
            //  (c-s-a) / (b-a) = (9-5-2) / (6-2) = 0.5
            var c = actionSet.AssignAndSave("animation_test_current_time", new V_TotalTimeElapsed()).Get();
            var t = actionSet.AssignAndSave("t", CaptureValue((currentActionTimeDelta * fps - keyframeA[0]) / (keyframeB[0] - keyframeA[0]), t => Element.TernaryConditional(currentActionShouldLoop, t % 1, Element.Part<V_Min>(t, new V_Number(1)))));
            // var t = actionSet.AssignAndSave("animation_t", 0);

            // Get the interpolated rotation.
            var slerp = AnimationOperations.Slerp(
                actionSet,
                keyframeA[1], keyframeA[2], // Quaternion A is at keyframeA[1] for XYZ and keyframeA[2] for W.
                keyframeB[1], keyframeB[2], // Quaternion B is at keyframeB[1] for XYZ and keyframeB[2] for W.
                t.Get()
            );

            var basis = actionSet.AssignAndSave("animation_matrix_basis", AnimationOperations.CreateColumnGrouped3x3MatrixFromQuaternion(slerp.V, slerp.W));

            // Make the resulting local matrix row-grouped.
            // After this is called, local can only be used in the left side of a matrix multiplication.
            actionSet.AddAction(local.SetVariable(ConvertArrayGroupedMatrixToRowGroupedMatrix(local.Get())));

            // Multiply the 'local' matrix by the 'basis' matrix.
            actionSet.AddAction(local.SetVariable(VectorNotatedMultiplyMatrix3x3AndMatrix3x3(local.Get(), basis.Get())));

            actionSet.AddAction(new A_End());

            // * At this point, 'local' and 'basis' are no longer required.
            // * 'matrixResult' is just basis.Get() to reuse a workshop variable.
            Element matrixResult = local.Get();

            // Save the matrix.
            actionSet.AddAction(localMatrices.SetVariable(ConvertArrayGroupedMatrixToRowGroupedMatrix(matrixResult), index:boneLoop.Value));

            actionLoop.Finish(); // End action loop

            actionSet.AddAction(Element.Part<A_SkipIf>(boneLoop.Value % 6, new V_Number(1)));
            actionSet.AddAction(A_Wait.MinimumWait);

            boneLoop.Finish(); // End the bone loop

            var newBoneLocalPositions = actionSet.AssignAndSave("newBoneLocalPositions", Element.Part<V_MappedArray>(
                BonePositions,
                Multiply3x3MatrixAndVectorToVector(localMatrices.Get()[BonePointsBone[new V_CurrentArrayIndex()]], new V_ArrayElement())
            ));

            actionSet.AddAction(A_Wait.MinimumWait);

            var newPosLoop = new ForRangeBuilder(actionSet, "newPosIndex", 0, Element.Part<V_CountOf>(newBoneLocalPositions.Get()), 1);
            newPosLoop.Init();
            var parent = BonePointParents[newPosLoop.Value];
            actionSet.AddAction(newBoneLocalPositions.ModifyVariable(
                Operation.Add, 
                Element.TernaryConditional(
                    // If the parent index is not equal to -1,
                    new V_Compare(parent, Operators.NotEqual, new V_Number(-1)),
                    // Add the parent's local position.
                    newBoneLocalPositions.Get()[parent],
                    // Otherwise, add zero.
                    V_Vector.Zero
                ),
                index: newPosLoop.Value
            ));
            newPosLoop.Finish();
            BoneLocalPositions = newBoneLocalPositions.Get();

            // UpdateActions(referenceLoop.Value, fps);

            // ! debug: only 1 tick
            // actionSet.AddAction(new A_Abort());

            actionSet.AddAction(new A_End()); // End armature
            referenceLoop.Finish(); // End object loop
            actionSet.AddAction(A_Wait.MinimumWait);
            actionSet.AddAction(new A_LoopIfConditionIsTrue());
        }

        public void AddAnimation(ActionSet actionSet, Element objectReference, Element actionIdentifier, Element loop)
        {
            Element actionIndex = actionSet.AssignAndSave("animation_action_index", GetActionIndexFromName(objectReference, actionIdentifier)).Get();

            var objectIndexInAnimationList = actionSet.AssignAndSave("animation_existing_obj_push", Element.Part<V_IndexOfArrayValue>(_animationReferenceList.Get(), objectReference));
            
            // If the _animationList array does not contain the object reference, add it to the list.
            actionSet.AddAction(Element.Part<A_If>(new V_Compare(objectIndexInAnimationList.Get(), Operators.Equal, new V_Number(-1))));

            // Empty index.
            actionSet.AddAction(_animationInfoList.SetVariable(new V_EmptyArray(), index: ReferenceListCount));
            
            // Push the objectReference.
            actionSet.AddAction(_animationReferenceList.ModifyVariable(Operation.AppendToArray, objectReference));

            // End the if
            actionSet.AddAction(new A_End());

            // [i, t]
            actionSet.AddAction(_animationInfoList.ModifyVariable(Operation.AppendToArray, Element.CreateArray(Element.CreateArray(actionIndex, new V_TotalTimeElapsed(), loop)), index: objectIndexInAnimationList.Get()));
        }

        public void StopAnimation(ActionSet actionSet, Element objectReference, Element actionIdentifier)
        {
            Element i = Element.Part<V_IndexOfArrayValue>(_animationReferenceList.Get(), objectReference),
                actionArray = _animationInfoList.Get()[i],
                currentActionInstance = actionArray[new V_ArrayElement()],
                actionIndex = GetActionIndexFromName(objectReference, actionIdentifier);

            // TODO: Make action remove queue.
            // For now, just remove it here.
            actionSet.AddAction(_animationInfoList.ModifyVariable(
                index: i,
                // Filter by index
                operation: Operation.RemoveFromArrayByIndex,
                value: Element.Part<V_FilteredArray>(
                    // Map to indices so the resulting array is the list of indices.
                    // [a1, a2, a3] => [0, 1, 2]
                    Element.Part<V_MappedArray>(actionArray, new V_CurrentArrayIndex()),
                    // Determines if the action was completed and is not set to loop.
                    new V_Compare(currentActionInstance[0], Operators.Equal, actionIndex)
                )
            ));
        }

        public void StopAllAnimations(ActionSet actionSet, Element objectReference)
        {
            Element i = Element.Part<V_IndexOfArrayValue>(_animationReferenceList.Get(), objectReference),
                actionArray = _animationInfoList.Get()[i];

            // TODO: Make action remove queue.
            // For now, just remove it here.
            actionSet.AddAction(_animationInfoList.SetVariable(index: i, value: new V_EmptyArray()));
        }

        private Element GetActionIndexFromName(Element objectReference, Element name) => Element.Part<V_IndexOfArrayValue>(_objectType.ActionNames.Get(objectReference), name);

        /// <summary>Removes actions from the action list when they finish playing.</summary>
        /// <param name="i">The current animation reference index.</param>
        /// <param name="fps">The FPS of the action. This should be removed later.</param>
        private void UpdateActions(Element i, Element fps)
        {
            Element actionArray =  _animationInfoList.Get()[i],
                // Get the action instance by doing _animationInfoList[ref][array element OR array index]
                // currentActionInstance[0] = action ID,
                // currentActionInstance[1] = start time,
                // currentActionInstance[2] = should loop?
                currentActionInstance = actionArray[new V_ArrayElement()],
                // Get the action source.
                currentActionSource = Actions[currentActionInstance[0]],
                // Get the action time and frame range.
                currentActionTimeDelta = new V_TotalTimeElapsed() - currentActionInstance[1],
                // The proceeding [0] is the action frame range as defined in 'BlendStructureHelper.GetAction()' in 'WorkshopDataConverter.cs'
                frameRange = currentActionSource[0],
                // Multiply 'currentActionTimeDelta' by 'fps' to get the frames per second.
                // TODO: 'fps' is currently hard-coded. Make it customizable to the user can control the animation playback speed.
                // * NOTE: 'fpsElapsed' is already calculated in the Loop() function, which means that
                // *       this same value is calculated multiple times.
                fpsElapsed = currentActionTimeDelta * fps,
                // Determines if the animation action was completed.
                completed = new V_Compare(fpsElapsed, Operators.GreaterThanOrEqual, frameRange);

            actionSet.AddAction(_animationInfoList.ModifyVariable(
                index: i,
                // Filter by index
                operation: Operation.RemoveFromArrayByIndex,
                value: Element.Part<V_FilteredArray>(
                    // Map to indices so the resulting array is the list of indices.
                    // [a1, a2, a3] => [0, 1, 2]
                    Element.Part<V_MappedArray>(actionArray, new V_CurrentArrayIndex()),
                    // Determines if the action was completed and is not set to loop.
                    Element.Part<V_And>(!currentActionInstance[2], completed)
                )
            ));
        }

        void DebugVariable(ActionSet actionSet, string name, Element value) => actionSet.AssignAndSave(name, value);

        Element ReferenceListCount => Element.Part<V_CountOf>(_animationReferenceList.Get());
    }
}