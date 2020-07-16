using AksAman.Extensions;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AksAman.Experiments
{
    public class UIPainter : MonoBehaviour
    {
        #region Painter_Variables
        [Header("UI Setup")]
        [SerializeField] int squareSize = 8;                        // Resolution of simulation box (Changed by UI Slider)
        [SerializeField] Transform horizontalLineHolder;            // Transform to hold horizontal gridlines as childs with HorizontalLayoutGroup
        [SerializeField] GameObject horizontalLinePrefab;           // Prefab for horizontal gridLine
        [SerializeField] Transform verticalLineHolder;              // Transform to hold Vertical gridlines as childs with VerticalLayoutGroup
        [SerializeField] GameObject verticalLinePrefab;             // Prefab for vertical gridline

        [Header("UI References")]
        [SerializeField] private Button drawButton;
        [SerializeField] private Button startButton;
        [SerializeField] Button computeButton;
        [SerializeField] Button randomizeButton;
        [SerializeField] Button cleanButton;
        [SerializeField] POTSlider potSlider;
        [SerializeField] TMP_Text squareSizeText;
        [SerializeField] TMP_Text stateText;
        [SerializeField] TMP_Dropdown simulationSpeedDropdown;

        /// <summary>
        /// Image component attached to this gameobject
        /// </summary>
        private Image image;

        /// <summary>
        /// RenderTextures created during start, used to transfer data between GPU and CPU
        /// </summary>
        private RenderTexture srcRenderTexture;
        private RenderTexture dstRenderTexture;

        /// <summary>
        /// RectTransform component attached to this gameobject
        /// </summary>
        private RectTransform rectTransform;

        /// <summary>
        /// Vector2Int holding current box size
        /// </summary>
        private Vector2Int textureSize;

        /// <summary>
        /// Texture modified during runtime painting or during randomization of pixels
        /// This is blitted to the current rendertexture to be computed by the compute shader
        /// </summary>
        private Texture inputTexture;

        /// <summary>
        /// Main Camera of the scene
        /// </summary>
        Camera mainCamera;

        /// <summary>
        /// Material assigned to the image component
        /// </summary>
        Material imageMaterial;

        /// <summary>
        /// Color to be assigned to dead pixels
        /// </summary>
        Color deadColor = Color.black;

        /// <summary>
        /// Starting pixels colors
        /// </summary>
        Color[] emptyColorsArray;

        /// <summary>
        /// Current Pixel colors
        /// </summary>
        Color32[] currentColors;
        #endregion

        #region Compute_Variables
        [Header("Compute stuff")]
        // The almighty Compute shader
        [SerializeField] ComputeShader computeShader;

        // Color to be assigned to alive cells (cannot be changed on runtime) (nneds r value to be always 1)
        [SerializeField] Color aliveColor = Color.white;

        /// <summary>
        /// This property is toggled continuously while compute shader is running
        /// if true, compute shader reads pixels from srcRenderTexture and assigns results to destRenderTexture
        /// else, vice versa
        /// </summary>
        private bool srcToDest = true;

        /// <summary>
        /// Kernel reference from the compute shader
        /// </summary>
        private int shaderKernel;

        /// <summary>
        /// bool to check if should run compute shader or not
        /// is false, while program is in drawing state
        /// </summary>
        private bool startCompute = false;

        /// <summary>
        /// Name of the kernel
        /// </summary>
        private const string kernelName = "GOL";
        #endregion

        private void Start()
        {
            // This to decide the simulation speed
            // A higher framerate gives faster simulation
            Application.targetFrameRate = 10;

            // Assigning image component reference
            image = GetComponent<Image>();

            // Assigning rectransform reference
            rectTransform = GetComponent<RectTransform>();

            // Assigning main camera reference
            mainCamera = Camera.main;

            // Assigning image material reference
            imageMaterial = image.material;

            // Set slider to initial square size
            potSlider.value = squareSize;

            // Set square size label to initial square size
            squareSizeText.text = squareSize.ToString();

            // Assigning actions to event listeners of buttons, sliders and dropdown
            AssignUIEvents();
            

        }

        /// <summary>
        /// Assigns actions to event listeners of buttons, sliders and dropdown
        /// </summary>
        private void AssignUIEvents()
        {
            //  Adding event to start button, onclick Sets up ui with appropriate size, grid and pixel colors
            startButton.onClick.AddListener(() => 
            {
                startButton.gameObject.SetActive(false);
                SetupUI();
            });

            // Adding event to power of two slider
            // updates ui and simulation box resolution on value change
            potSlider.onValueChanged.AddListener((value) =>
            {
                squareSize = (int)value;
                squareSizeText.text = ((int)value).ToString();
                SetupUI();

            });

            // Adding event to Compute ui button, sets startCompute to true, and state text to "COMPUTE"
            computeButton.onClick.AddListener(
                () => { startCompute = true; stateText.text = "STATE : COMPUTE"; }
            );

            // Adding event to Draw ui button, sets startCompute to false, and state text to "DRAW"
            drawButton.onClick.AddListener(
                () => { startCompute = false; stateText.text = "STATE : DRAW"; 
            });

            // Adding event to Randomize ui button, setups the ui with randomized pixels, and state text to "DRAW"
            randomizeButton.onClick.AddListener(
                () => { SetupUI(random: true); stateText.text = "STATE : DRAW"; }
            );

            // Adding event to Clean ui button, setups the ui with all dead pixels, and state text to "DRAW"
            cleanButton.onClick.AddListener(
                () => { SetupUI(random: false); stateText.text = "STATE : DRAW"; }
            );

            // Adding event to SimulationSpeedDropdown", which changes the speed of simulation (Obviously)
            simulationSpeedDropdown.onValueChanged.AddListener((state) =>
            {
                // switches current value of dropdown and changes application's framerate
                // which inturns affects simulation speed
                switch (state)
                {
                    case 0:
                        // slow
                        Application.targetFrameRate = 10;
                        break;
                    case 1:
                        // medium
                        Application.targetFrameRate = 50;
                        break;
                    case 2:
                        // fast
                        Application.targetFrameRate = 1000;
                        break;
                    default:
                        break;
                }
            });
        }

        /// <summary>
        /// Sets up the ui and does other initializations
        /// </summary>
        /// <param name="random">Should we randomize the pixels or, you need a clean slate</param>
        private void SetupUI(bool random = true)
        {
            // Changes UI state text to "DRAW", as we always reset to draw state whenever we resetup everything
            stateText.text = "STATE : DRAW";

            // Sets up the rectTransform width and height to squareSize
            rectTransform.sizeDelta = Vector2.one * squareSize;

            // Sets up the scale so that the rectTransform always fits the 1024x1024 area
            rectTransform.localScale = Vector3.one * (1024.0f / squareSize);

            // Create grid
            CreateGrid();

            // sets texturesize property
            textureSize = new Vector2Int(squareSize, squareSize);

            //// Releases existing renderTextures
            if (srcRenderTexture != null) srcRenderTexture.Release();
            if (srcRenderTexture != null) dstRenderTexture.Release();

            //// Create renderTextures to be used by computeshader to process
            CreateRenderTexture(out srcRenderTexture);
            CreateRenderTexture(out dstRenderTexture);

            //// Populate empty color array
            MakeEmptyColorsArray(random);

            //// set pixels of inputTexture from emptyColorsArray
            ResetSprite();

            //// Copy contents of inputTexture to appropriate renderTexture
            Graphics.Blit(inputTexture, (srcToDest ? srcRenderTexture : dstRenderTexture));

            //// set material maintexture
            imageMaterial.mainTexture = (srcToDest ? srcRenderTexture : dstRenderTexture);

            //// Initialize computeshader
            InitShader();

            //// Refreshes ui image as sometimes unity doesn't
            RefreshImage();
        }

        /// <summary>
        /// Initializes the compute shader and kernels
        /// </summary>
        private void InitShader()
        {
            if (computeShader != null)
            {
                startCompute = false;

                // Find proper kernel from the compute shader
                shaderKernel = computeShader.FindKernel(kernelName);

                // Assigns parameters in compute shader
                computeShader.SetFloat("Width", textureSize.x);
                computeShader.SetFloat("Height", textureSize.y);
                computeShader.SetTexture(shaderKernel, "Input", inputTexture);
                computeShader.SetTexture(shaderKernel, "Result", srcRenderTexture);
                computeShader.SetVector("aliveColor", new Vector4(1, aliveColor.g, aliveColor.b, aliveColor.a));
            }
        }



        private void Update()
        {
            // Check if user has pressed mouseButton "and" startCompute is false
            if (Input.GetMouseButtonDown(0) && !startCompute)
            {
                // Current mousePosition in ScreenSpace
                Vector2 currentMouseScreenPosition = Input.mousePosition;

                // Check if user's mouse cursor is within this rect bounds
                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, currentMouseScreenPosition))
                {
                    Vector2 localCursor;
                    // RectTransformUtility method to transform screen point to local coordinates
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, currentMouseScreenPosition, null, out localCursor))
                    {
                        // Paint at given pixel coordinate
                        Paint(
                            // Transform local coordinate to pixel coordinate
                            LocalToPixelCoords(localCursor)
                        );
                    }
                }
            }
            else
            {
                // Check if startCompute is true
                if(startCompute)
                {
                    // Compute next iteration of gameoflife
                    ComputeGOL();
                }
            }
        }

        /// <summary>
        /// Paints at given pixel position
        /// </summary>
        /// <param name="pixelPosition">Position in pixel coordinates to paint at</param>
        private void Paint(Vector2Int pixelPosition)
        {
            // renderTexture holds current material mainTexture
            RenderTexture renderTexture = (RenderTexture)imageMaterial.mainTexture;

            // Convert renderTexture to Texture2D
            // This has to be done because we need to pixels of the current material texture
            // which is not possible using a render texture
            Texture2D tex = renderTexture.RT2Texture2D();

            // Store current pixel values in currentColors Array
            // GetPixels32() has been used instead of GetPixels(), because the former is faster
            currentColors = tex.GetPixels32();

            // Get flattened 1D arrayLocation from pixel coord position
            // Logic : currentX + width*CurrentY;
            int flattenedPos = pixelPosition.y * textureSize.y + pixelPosition.x;

            // Check if flattened position exceeds array length or is negative
            if (flattenedPos >= currentColors.Length || flattenedPos < 0)
            {
                // In that case, refresh the ui image and just return
                RefreshImage();
                return;
            }

            // Set pixel at flattened position in currentColor to appropriate color
            // If user is holding shift while clicking, the cell is painted as dead cell, else as alive cell
            if(Input.GetKey(KeyCode.LeftShift))
                currentColors[flattenedPos] = deadColor;
            else
                currentColors[flattenedPos] = aliveColor;

            // Writeback modified pixel values to the texture
            tex.SetPixels32(currentColors);

            // Apply the changes on the texture (just setting the pixels won't work :)
            tex.Apply();

            // Release the temporary renderTexture
            renderTexture.Release();
            
            // Copy contents of current modified local texture to material's main texture
            Graphics.Blit(tex, (RenderTexture)imageMaterial.mainTexture);

            // Destroy local texture
            Destroy(tex);

            // Refresh UI Image
            RefreshImage();
        }


        /// <summary>
        /// Actual GameOfLife compute shader execution code (cpu side)
        /// </summary>
        private void ComputeGOL()
        {
            // Here we are constantly swapping renderTextures as input and result textures
            // for nth iteration, if input is srcRenderTexture, calculation is done on it, and written on result which is the dstRenderTexture
            // for (n+1)th iteration, dstRenderTexture will become the input texture and srcRenderTexture becomes the result
            if(srcToDest)
            {
                // Set compute shader input texture to srcRenderTexture
                computeShader.SetTexture(shaderKernel, "Input", srcRenderTexture);

                // Set compute shader resulting texture to dstRenderTexture
                computeShader.SetTexture(shaderKernel, "Result", dstRenderTexture);

                // Dispatches (or executes) the compute shader
                // Takes x, y, and z number of groups as parameters
                // IMP: If you see the compute shader code, it has [numthreads(8,8,1)], which are sizes of threadgroup in a particular direction
                // So, if textureSize is (1024,1024), it will dispatch 1024/8 = 128 x and y groups, both having 8 threads each
                computeShader.Dispatch(shaderKernel, textureSize.x / 8, textureSize.y / 8, 1);

                // sets dstRenderTexture as material's main texture
                imageMaterial.mainTexture = dstRenderTexture;
            }
            else
            {
                // Same stuff as above, but reversed renderTextures

                computeShader.SetTexture(shaderKernel, "Input", dstRenderTexture);
                computeShader.SetTexture(shaderKernel, "Result", srcRenderTexture);
                computeShader.Dispatch(shaderKernel, textureSize.x / 8, textureSize.y / 8, 1);
                imageMaterial.mainTexture = srcRenderTexture;
            }
            // toggle bool srcToDest
            srcToDest = !srcToDest;

            // Refresh UI Image
            RefreshImage();

        }

        #region HELPER_CODE
        /// <summary>
        /// Poplulates the emptyColorArray with emptyColor
        /// </summary>
        private void MakeEmptyColorsArray(bool shouldRandomize)
        {
            emptyColorsArray = new Color[textureSize.x * textureSize.y];
            for (int i = 0; i < emptyColorsArray.Length; i++)
            {
                // generating random number between 0 and n (Higher n => lower probabilty of alive cell)
                int random = UnityEngine.Random.Range(0, 8);

                // if shouldRandomize, set random color as aliveColor if random number is 0, else deadColor
                emptyColorsArray[i] = shouldRandomize? (random != 0? deadColor : aliveColor) : deadColor;
            }
        }

        /// <summary>
        /// Resets the sprite texture with emptyColorArray
        /// </summary>
        private void ResetSprite()
        {
            // Creates Texture2D inputTexture after destroying existing ones
            if (inputTexture != null) Destroy(inputTexture);
            inputTexture = new Texture2D(textureSize.x, textureSize.y);

            // Sets texture pixels to emptyColorsArray
            ((Texture2D)inputTexture).SetPixels(emptyColorsArray);
            ((Texture2D)inputTexture).Apply();
        }

        /// <summary>
        /// Transforms world ui coordinate to pixel coordinates
        /// </summary>
        /// <param name="_worldPosition">position in world coordinates</param>
        /// <returns>position in pixel coordinates</returns>
        private Vector2Int WorldToPixelCoords(Vector2 _worldPosition)
        {
            // Transforms world coordinate to local coordinates
            Vector3 localPostion = transform.InverseTransformPoint(_worldPosition);
            return LocalToPixelCoords(localPostion);
        }

        /// <summary>
        /// Transforms local ui coordinates to pixel coordinates
        /// </summary>
        /// <param name="_localPosition">Position in local coordinates</param>
        /// <returns>Integer Position in pixel coordinates</returns>
        private Vector2Int LocalToPixelCoords(Vector2 _localPosition)
        {
            float pixelWidth = textureSize.x;
            float pixelHeight = textureSize.y;

            // Centering coordinates, as currently the local postition is between
            // (-width, -height) to (width, height)
            float centeredX = _localPosition.x + pixelWidth / 2;
            float centeredY = _localPosition.y + pixelHeight / 2;

            // Flooring floating coordinates to integral positions
            Vector2Int pixelPosition = new Vector2Int(Mathf.FloorToInt(centeredX), Mathf.FloorToInt(centeredY));

            return pixelPosition;
        }

        /// <summary>
        /// Creates RenderTexture with enableRandomWrite as true
        /// </summary>
        /// <param name="renderTexture">output renderTexture</param>
        private void CreateRenderTexture(out RenderTexture renderTexture)
        {
            renderTexture = new RenderTexture(textureSize.x, textureSize.y, 24);
            renderTexture.wrapMode = TextureWrapMode.Repeat;
            renderTexture.enableRandomWrite = true;
            renderTexture.filterMode = FilterMode.Point;
            renderTexture.useMipMap = false;
            renderTexture.Create();
        }

        /// <summary>
        /// Clears existing grid and creates new one
        /// </summary>
        private void CreateGrid()
        {
            // clear
            horizontalLineHolder.ClearTransformChildren();
            verticalLineHolder.ClearTransformChildren();

            // create
            for (int i = 0; i < squareSize; i++)
            {
                Instantiate(horizontalLinePrefab, horizontalLineHolder);
                Instantiate(verticalLinePrefab, verticalLineHolder);
            }
        }




        /// <summary>
        /// Quickly disable and enable ui image to refresh changes
        /// </summary>
        private void RefreshImage()
        {
            image.enabled = false;
            image.enabled = true;
        }


        #endregion

    }
}
