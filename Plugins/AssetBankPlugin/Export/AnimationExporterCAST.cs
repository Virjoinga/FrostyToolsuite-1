using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetBankPlugin.Export
{
    public class AnimationExporterCAST : IAnimationExporter
    {
        public override void Export(InternalAnimation animation, InternalSkeleton skeleton, string path)
        {
            Directory.CreateDirectory(path);
            string filePath = Path.Combine(path, animation.Name + ".cast");

            var castFile = new Cast();
            var root = castFile.CreateRoot();

            var metadata = root.CreateMetadata();
            metadata.SetSoftware("Frosty Engine Animation Exporter");
            metadata.SetUpAxis("Y");

            var castAnim = root.CreateAnimation();
            castAnim.SetName(animation.Name);
            castAnim.SetFramerate(30.0f);    
            castAnim.SetLooping(false);

            var sortedFrames = animation.Frames.OrderBy(f => f.FrameIndex).ToList();
            if (sortedFrames.Count == 0)
                return;

            List<uint> keyframes = sortedFrames.Select(f => (uint)f.FrameIndex).ToList();

            for (int rotChIdx = 0; rotChIdx < animation.RotationChannels.Count; rotChIdx++)
            {
                string boneName = animation.RotationChannels[rotChIdx];
                var curve = castAnim.CreateCurve();
                curve.SetNodeName(boneName);
                curve.SetKeyPropertyName("rq");
                curve.SetMode(animation.Additive ? "additive" : "absolute");
                curve.SetKeyFrameBuffer(keyframes);

                List<float[]> rotValues = new List<float[]>(sortedFrames.Count);
                foreach (var frame in sortedFrames)
                {
                    if (rotChIdx < frame.Rotations.Count)
                    {
                        var q = frame.Rotations[rotChIdx];
                        rotValues.Add(new float[] { q.X, q.Y, q.Z, q.W });
                    }
                    else
                    {
                        rotValues.Add(new float[] { 0.0f, 0.0f, 0.0f, 1.0f });    
                    }
                }
                curve.SetVec4KeyValueBuffer(rotValues);
            }

            string[] translationAxes = { "tx", "ty", "tz" };
            string[] scaleAxes = { "sx", "sy", "sz" };

            for (int posChIdx = 0; posChIdx < animation.PositionChannels.Count; posChIdx++)
            {
                string boneName = animation.PositionChannels[posChIdx];

                for (int axis = 0; axis < 3; axis++)
                {
                    var curve = castAnim.CreateCurve();
                    curve.SetNodeName(boneName);
                    curve.SetKeyPropertyName(translationAxes[axis]);
                    curve.SetMode(animation.Additive ? "additive" : "absolute");
                    curve.SetKeyFrameBuffer(keyframes);

                    List<float> values = new List<float>(sortedFrames.Count);
                    foreach (var frame in sortedFrames)
                    {
                        if (posChIdx < frame.Positions.Count)
                        {
                            var p = frame.Positions[posChIdx];
                            switch (axis)
                            {
                                case 0: values.Add(p.X); break;
                                case 1: values.Add(p.Y); break;
                                case 2: values.Add(p.Z); break;
                            }
                        }
                        else
                        {
                            values.Add(0.0f);    
                        }
                    }
                    curve.SetFloatKeyValueBuffer(values);
                }
            }

            for (int sclChIdx = 0; sclChIdx < animation.ScaleChannels.Count; sclChIdx++)
            {
                string boneName = animation.ScaleChannels[sclChIdx];

                for (int axis = 0; axis < 3; axis++)
                {
                    var curve = castAnim.CreateCurve();
                    curve.SetNodeName(boneName);
                    curve.SetKeyPropertyName(scaleAxes[axis]);
                    curve.SetMode(animation.Additive ? "additive" : "absolute");
                    curve.SetKeyFrameBuffer(keyframes);

                    List<float> values = new List<float>(sortedFrames.Count);
                    foreach (var frame in sortedFrames)
                    {
                        if (sclChIdx < frame.Scales.Count)
                        {
                            var s = frame.Scales[sclChIdx];
                            switch (axis)
                            {
                                case 0: values.Add(s.X); break;
                                case 1: values.Add(s.Y); break;
                                case 2: values.Add(s.Z); break;
                            }
                        }
                        else
                        {
                            values.Add(1.0f);    
                        }
                    }
                    curve.SetFloatKeyValueBuffer(values);
                }
            }

            castFile.Save(filePath);
        }
    }
}