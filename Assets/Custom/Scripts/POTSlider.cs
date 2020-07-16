using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AksAman.Experiments
{
    /// <summary>
    /// Custom slider inherited from <see cref="UnityEngine.UI.Slider"/>
    /// This snaps values to only power of two (Exa : 1,2,4,8,16,...,1024 etc)
    /// </summary>
    public class POTSlider : Slider
    {
        /// <summary>
        /// Overriding UnityEngine.UI.<see cref="UnityEngine.UI.Slider.Set(float, bool)"/> method
        /// to snap to values which are powers of two
        /// </summary>
        /// <param name="input">Slider original value</param>
        /// <param name="sendCallback"></param>
        protected override void Set(float input, bool sendCallback = true)
        {
            //Setting slider original value to nearest power of two
            input = Mathf.NextPowerOfTwo((int)input);

            base.Set(input, sendCallback);
        }
    } 
}
