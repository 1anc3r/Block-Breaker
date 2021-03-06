﻿using System.IO;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

// 游戏控制器
public class GameController : MonoBehaviour
{
    public Font font;
    public GameObject BulletBall; // BulletBall预制体
    public GameObject BlockBall; // BlockBall预制体
    public GameObject BlockCube; // BlockCube预制体
    public GameObject scoreText;
    public GameObject quantityText;
    public GameObject playButton;
    public GameObject exitButton;
    private Image background;
    private AspectRatioFitter fitter;
    private int frame = 0; // 游戏帧，用于控制射速
    private int score = 0; // 游戏分数
    private int layer = 0; // 层数
    private int capacity = 5; // 弹容量
    private int quantity = 5; // 弹余量
    private GameStatus status = GameStatus.Init; // 游戏状态
    private LayerMask layerMask; // 层遮罩，用于瞄准
    private Vector3 launcher; // 发射架
    private Vector3 direction = Vector3.zero; // 发射方向
    private List<GameObject> bullets;
    private List<GameObject> blocks;

    void Start()
    {
        launcher = new Vector3(0f, 4.1f, 0f);
        layerMask = 1 << (LayerMask.NameToLayer("Plane"));
        bullets = new List<GameObject>();
        blocks = new List<GameObject>();
        background = GameObject.Find("Background Image").GetComponent<Image>();
        fitter = GameObject.Find("Background Image").GetComponent<AspectRatioFitter>();
        SetBackgroundByUrl(Path.Combine(Application.streamingAssetsPath, "Background.jpg"));
        playButton.GetComponent<Button>().onClick.AddListener(OnGamePlayClick);
        exitButton.GetComponent<Button>().onClick.AddListener(OnGameExitClick);
    }

    void FixedUpdate()
    {
        switch (status)
        {
            case GameStatus.Reloading:
                // 装弹
                PressAllBlocks();
                PlaceAllBlocks();
                break;
            case GameStatus.Aiming:
                // 瞄准
                Aim();
                break;
            case GameStatus.Launching:
                // 发射
                if (frame % 10 == 0)
                {
                    LaunchOneBullet();
                }
                break;
        }
        frame = (frame == 1200) ? 0 : ++frame;
    }

    private void OnGamePlayClick()
    {
        GamePlay();
    }

    private void OnGameExitClick()
    {
        Application.Quit();
    }

    // 游戏开始
    public void GamePlay()
    {
        score = 0;
        frame = 0;
        scoreText.SetActive(true);
        quantityText.SetActive(true);
        playButton.SetActive(false);
        exitButton.SetActive(false);
        status = GameStatus.Aiming;
        PlaceAllBlocks();
    }

    // 游戏结束
    public void GameOver()
    {
        scoreText.SetActive(false);
        quantityText.SetActive(false);
        playButton.SetActive(true);
        exitButton.SetActive(true);
        status = GameStatus.Init;
        ClearAllBullets();
        ClearAllBlocks();
    }

    // 清空屏幕上的Bullet
    private void ClearAllBullets()
    {
        foreach (GameObject bulletBall in bullets)
        {
            if (bulletBall != null)
            {
                Destroy(bulletBall);
            }
        }
        bullets.Clear();
    }

    // 清空屏幕上的Block
    private int ClearAllBlocks()
    {
        int score = 0;
        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                score += block.GetComponent<BlockController>().GetScore();
                Destroy(block);
            }
        }
        blocks.Clear();
        return score;
    }

    // 增加分数
    public void AddScore(int score)
    {
        this.score += score;
        scoreText.GetComponent<TextMeshProUGUI>().text = "Score:" + this.score;
    }

    // 增加弹容量
    public void AddCapacity()
    {
        capacity++;
    }

    // 通过bytes设置背景图片
    public void SetBackgroundByBytes(byte[] bytes)
    {
        try
        {
            Texture2D texture = new Texture2D(Screen.width, Screen.height);
            fitter.aspectRatio = texture.width * 1f / texture.height;
            texture.LoadImage(bytes);
            background.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
        }
        catch (System.Exception e)
        {
            Debug.Log("Set background failed. Error: " + e.Message);
            throw e;
        }
    }

    // 切换背景颜色，同步接口
    public void SetBackgroundColor(Color color)
    {
        try
        {
            background.color = color;
        }
        catch (System.Exception e)
        {
            Debug.Log("Set background color failed. Error: " + e.Message);
            throw e;
        }
    }

    // 切换背景图片，异步接口
    public void SetBackgroundByUrl(string path)
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            path = "file://" + path;
        }
        StartCoroutine(SetBackgroundByUrlAsync(path));
    }

    // 通过url/path切换背景
    private IEnumerator SetBackgroundByUrlAsync(string path)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(path, true))
        {
            yield return request.SendWebRequest();
            if (request.responseCode == 200)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                fitter.aspectRatio = texture.width * 1f / texture.height;
                background.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
            }
            else
            {
                Debug.Log("Set " + path + " to background failed. Error: " + request.error);
            }
        }
    }

    // 瞄准
    private void Aim()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        if (Input.GetMouseButtonUp(0))
#elif UNITY_IPHONE || UNITY_ANDROID
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended)
#endif
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo = new RaycastHit();
            if (Physics.Raycast(ray, out hitInfo, Mathf.Infinity, layerMask.value))
            {
                Debug.DrawLine(ray.origin, hitInfo.point);
                direction = (hitInfo.point - launcher).normalized;
                direction.z = 0;
                status = GameStatus.Launching;
            }
        }
    }

    // 重新装填
    private void Reload()
    {
        frame = 1;
        quantity = capacity;
        quantityText.GetComponent<TextMeshProUGUI>().text = quantity.ToString();
    }

    // 发射一个球
    private void LaunchOneBullet()
    {
        if (quantity > 0)
        {
            GameObject bulletBall = Instantiate(BulletBall, launcher, Quaternion.identity);
            bulletBall.transform.name = "Bullet Ball";
            bulletBall.GetComponent<Rigidbody>().velocity = direction * 20f;
            bullets.Add(bulletBall);
            quantity--;
            quantityText.GetComponent<TextMeshProUGUI>().text = quantity.ToString();
        }
        else
        {
            CheckAllBullets();
        }
    }

    // 检测所有球是否在屏幕上
    private void CheckAllBullets()
    {
        foreach (GameObject bulletBall in bullets)
        {
            if (bulletBall != null)
            {
                return;
            }
        }
        bullets.Clear();
        status = GameStatus.Reloading;
        Reload();
    }

    // 放置一堆Block
    private void PlaceAllBlocks()
    {
        float range = Mathf.Abs(Random.Range(-3f, 3f));
        if (range > 0 && range <= 1.25)
        {
            PlaceOneBlock(0.95f, 1.85f);
        }
        if (range > 0.75 && range < 2.25)
        {
            PlaceOneBlock(-0.45f, 0.45f);
        }
        if (range >= 2.25)
        {
            PlaceOneBlock(-1.85f, -0.95f);
        }
    }

    // 放置一个Block
    private void PlaceOneBlock(float left, float right)
    {
        Vector3 position = new Vector3(Random.Range(left == 0f ? -2f : left, right == 0f ? 2f : right), -5f, 0f);
        GameObject block;
        int number = (score + 50) / 10;
        if (layer % 5 == 0)
        {
            block = Instantiate(BlockBall, position, Quaternion.identity);
            if (layer % 5 == 0)
            {
                block.GetComponent<BlockController>().Init(1, 1);
            }
        }
        else
        {
            block = Instantiate(BlockCube, position, Quaternion.identity);
            block.GetComponent<BlockController>().Init(0, number);
            block.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        }
        block.transform.name = "Block";
        blocks.Add(block);
    }

    // 推进所有砖
    private void PressAllBlocks()
    {
        foreach (GameObject block in blocks)
        {
            if (block != null)
            {
                Vector3 target = block.transform.position;
                target.y += 1;
                block.transform.position = Vector3.MoveTowards(block.transform.position, target, 0.7f);
                block.GetComponent<BlockController>().Press();
            }
        }
        layer++;
        status = GameStatus.Aiming;
    }
}
