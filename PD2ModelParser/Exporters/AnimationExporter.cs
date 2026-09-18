using PD2ModelParser.Misc;
using PD2ModelParser.Sections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PD2ModelParser.Exporters {
    internal class AnimationExporter {
        public static string ExportFile(FullModelData data, string path) {
            AnimationFile animationFile = new();

            foreach (Object3D object3D in data.SectionsOfType<Object3D>()) {
                if (object3D.Animations.Count > 0) {
                    AnimationFileObject animationFileObject = new(object3D.HashName.String);

                    foreach (IAnimationController animationController in object3D.Animations) {
                        if (animationController is LinearVector3Controller linearVector3Controller)
                        {
                            animationFileObject.PositionKeyframes = [.. linearVector3Controller.Keyframes];
                        }
                        else if (animationController is QuatLinearRotationController quatLinearRotationController)
                        {
                            animationFileObject.RotationKeyframes = [.. quatLinearRotationController.Keyframes];
                        }
                    }

                    animationFile.Objects.Add(animationFileObject);
                }
            }

            animationFile.Write(path);

            return path;
        }
    }
}
