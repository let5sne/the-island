using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

/// <summary>
/// 中文字体设置工具
/// 在 Unity 菜单中: Tools > Setup Chinese Font
/// </summary>
public class ChineseFontSetup : EditorWindow
{
    [MenuItem("Tools/Setup Chinese Font")]
    public static void ShowWindow()
    {
        GetWindow<ChineseFontSetup>("中文字体设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("中文字体设置向导", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "此工具将帮助您设置 TextMeshPro 的中文字体支持。\n\n" +
            "步骤：\n" +
            "1. 点击下方按钮生成 TMP 字体资产\n" +
            "2. 等待生成完成\n" +
            "3. 将生成的字体设置为 TMP 默认字体的 Fallback",
            MessageType.Info);

        GUILayout.Space(20);

        if (GUILayout.Button("1. 生成中文字体资产", GUILayout.Height(40)))
        {
            CreateChineseFontAsset();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("2. 设置为默认 Fallback", GUILayout.Height(40)))
        {
            SetupFallbackFont();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("3. 打开 TMP 设置", GUILayout.Height(30)))
        {
            // 打开 TMP Settings
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null)
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
            else
            {
                EditorUtility.DisplayDialog("提示",
                    "请先通过 Window > TextMeshPro > Import TMP Essential Resources 导入 TMP 资源",
                    "确定");
            }
        }
    }

    private void CreateChineseFontAsset()
    {
        // 查找字体文件 (思源黑体)
        string fontPath = "Assets/Fonts/SourceHanSansSC-Regular.otf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

        if (sourceFont == null)
        {
            EditorUtility.DisplayDialog("错误",
                $"找不到字体文件: {fontPath}\n请确保字体文件存在。",
                "确定");
            return;
        }

        // 常用中文字符集 (约 3000 个常用字)
        string chineseChars = GetCommonChineseCharacters();

        // 创建字体资产
        string outputPath = "Assets/Fonts/SourceHanSansSC-Regular SDF.asset";

        // 检查是否已存在
        if (File.Exists(outputPath))
        {
            if (!EditorUtility.DisplayDialog("确认",
                "字体资产已存在，是否覆盖？",
                "覆盖", "取消"))
            {
                return;
            }
        }

        EditorUtility.DisplayProgressBar("生成字体", "正在创建 TMP 字体资产...", 0.3f);

        try
        {
            // 使用 TMP 的字体资产创建器
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,  // Sampling point size
                9,   // Padding
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, // Atlas width
                1024  // Atlas height
            );

            if (fontAsset != null)
            {
                // 保存字体资产
                AssetDatabase.CreateAsset(fontAsset, outputPath);

                // 添加字符
                EditorUtility.DisplayProgressBar("生成字体", "正在添加中文字符...", 0.6f);

                // 尝试添加字符
                uint[] unicodeArray = new uint[chineseChars.Length];
                for (int i = 0; i < chineseChars.Length; i++)
                {
                    unicodeArray[i] = chineseChars[i];
                }

                fontAsset.TryAddCharacters(chineseChars, out string missingChars);

                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("成功",
                    $"字体资产已创建: {outputPath}\n\n" +
                    $"添加了 {chineseChars.Length - (missingChars?.Length ?? 0)} 个字符\n" +
                    "请点击 '设置为默认 Fallback' 完成配置。",
                    "确定");

                // 选中创建的资产
                Selection.activeObject = fontAsset;
                EditorGUIUtility.PingObject(fontAsset);
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "创建字体资产失败", "确定");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private void SetupFallbackFont()
    {
        // 加载中文字体资产
        string fontAssetPath = "Assets/Fonts/SourceHanSansSC-Regular SDF.asset";
        TMP_FontAsset chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);

        if (chineseFont == null)
        {
            EditorUtility.DisplayDialog("错误",
                "找不到中文字体资产，请先点击 '生成中文字体资产'",
                "确定");
            return;
        }

        // 加载 TMP 设置
        TMP_Settings tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
        if (tmpSettings == null)
        {
            EditorUtility.DisplayDialog("错误",
                "找不到 TMP Settings，请先导入 TMP Essential Resources\n" +
                "(Window > TextMeshPro > Import TMP Essential Resources)",
                "确定");
            return;
        }

        // 获取默认字体资产 (静态属性)
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
        {
            EditorUtility.DisplayDialog("错误", "TMP 默认字体未设置", "确定");
            return;
        }

        // 检查是否已经添加了 fallback
        if (defaultFont.fallbackFontAssetTable != null &&
            defaultFont.fallbackFontAssetTable.Contains(chineseFont))
        {
            EditorUtility.DisplayDialog("提示", "中文字体已经是默认字体的 Fallback", "确定");
            return;
        }

        // 添加为 fallback
        if (defaultFont.fallbackFontAssetTable == null)
        {
            defaultFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
        }
        defaultFont.fallbackFontAssetTable.Add(chineseFont);

        EditorUtility.SetDirty(defaultFont);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("成功",
            $"已将 {chineseFont.name} 添加为默认字体的 Fallback\n" +
            "现在中文字符应该可以正常显示了！",
            "确定");
    }

    /// <summary>
    /// 获取常用中文字符集
    /// 包含：基本标点、数字、常用汉字约3000个
    /// </summary>
    private string GetCommonChineseCharacters()
    {
        // ASCII 基本字符
        string ascii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        // 中文标点
        string punctuation = "，。！？、；：\u201C\u201D\u2018\u2019【】（）《》—…·";

        // 常用汉字 (GB2312 一级汉字 + 游戏常用词汇)
        string commonHanzi =
            "的一是不了在人有我他这个们中来上大为和国地到以说时要就出会可也你对生能而子那得于着下自之年过发后作里用道行所然家种事成方多经么去法学如都同现当没动面起看定天分还进好小部其些主样理心她本前开但因只从想实日军者意无力它与长把机十民第公此已工使情明性知全三又关点正业外将两高间由问很最重并物手应战向头文体政美相见被利什二等产或新己制身果加西斯月话合回特代内信表化老给世位次度门任常先海通教儿原东声提立及比员解水名真论处走义各入几口认条平系气题活尔更别打女变四神总何电数安少报才结反受目太量再感建务做接必场件计管期市直德资命山金指克许统区保至队形社便空决治展马科司五基眼书非则材率末写军导每非则材料战争世界历史技术管理经济社会文化教育科学研究发展建设改革开放创新服务质量效率安全健康环境保护资源能源交通通信网络信息数据系统设备技术标准规范要求目标任务计划方案措施办法政策法规制度机构组织部门单位企业公司集团项目工程产品市场客户用户消费需求供应生产加工制造设计开发测试维护运营销售采购物流仓储配送安装调试培训支持咨询顾问专家团队成员领导负责主管经理总监董事股东投资融资财务会计审计税务法律合同协议订单发票报表统计分析评估预测规划调度监控报警故障问题解决优化改进升级更新版本功能模块接口协议格式编码加密压缩传输存储备份恢复删除修改添加查询搜索排序过滤分组汇总合计平均最大最小总数数量金额价格成本利润收入支出余额账户密码登录注销注册验证授权权限角色用户名邮箱手机地址城市省份国家语言时区货币单位格式日期时间年月日时分秒周星期今天明天昨天早上中午下午晚上凌晨上午下午傍晚夜间春夏秋冬东南西北前后左右上下内外高低大小长短宽窄厚薄轻重快慢新旧好坏对错是否有无多少全部部分其他更多更少最多最少第一第二第三最后开始结束继续暂停停止取消确认提交保存关闭返回退出进入打开切换选择点击双击拖动滚动缩放旋转移动复制粘贴剪切撤销重做刷新加载下载上传导入导出打印预览分享发送接收回复转发收藏删除编辑查看详情简介说明帮助提示警告错误成功失败完成进度状态在线离线忙碌空闲可用不可用启用禁用显示隐藏展开折叠锁定解锁同步异步自动手动默认自定义高级基本简单复杂普通特殊正常异常临时永久公开私有共享独占只读可写执行调用请求响应返回参数变量常量属性方法函数类对象实例数组列表字典集合队列堆栈树图节点边路径文件夹目录根子父兄弟祖先后代深度宽度高度长度大小位置坐标角度方向速度加速度力矩能量功率电压电流电阻频率周期波长振幅相位温度湿度压力密度质量体积面积周长直径半径圆方三角矩形菱形椭圆球体立方锥体柱体点线面体红橙黄绿蓝紫黑白灰棕粉金银铜铁钢铝锌铅锡镍钴钨钼钛铬锰钒硅碳氮氧氢氦氖氩氪氙氡氟氯溴碘硫磷硼砷硒碲钋砹水火土木金风雷电冰雪雨雾云霜露霞虹日月星辰天空大地山川河流湖海洋岛屿森林草原沙漠戈壁高原盆地平原丘陵峡谷瀑布泉眼火山地震海啸台风龙卷洪涝干旱饥荒瘟疫战乱和平统一分裂独立解放革命改良维新复兴崛起衰落灭亡重生涅槃永恒瞬间刹那须臾片刻良久许久很久永远从来一直始终早已已经将要即将正在仍然依然还是照样如同好像似乎仿佛几乎差点险些恰好刚好正好偏偏偏要非要硬要死活无论不管尽管即使哪怕万一假如倘若要是除非只要只有一旦每当每次每回每当凡是所有任何某些这些那些哪些另外此外另一其余剩余多余富余盈余亏损赤字顺差逆差平衡均衡协调配合合作竞争对抗冲突矛盾争议纠纷诉讼仲裁调解和解妥协让步坚持固执倔强顽固执着专注集中分散注意忽视重视轻视鄙视歧视尊重敬仰崇拜信仰虔诚忠诚忠实诚实诚恳真诚热情热心热爱喜欢喜爱爱好兴趣乐趣趣味有趣无趣枯燥乏味单调重复循环往复反复再三三番五次屡次多次偶尔有时经常总是永远从不难得罕见常见普遍广泛狭隘偏僻边远遥远临近附近周围四周环绕包围围困困扰烦恼苦恼忧愁忧虑担忧担心害怕恐惧惊恐惊慌惊讶惊喜欣喜高兴快乐幸福美满圆满完满满意满足知足贪婪贪心贪欲欲望渴望期望希望盼望向往憧憬梦想幻想空想妄想奢望绝望失望悲观乐观积极消极主动被动进攻防守攻击袭击反击还击出击突击冲击打击撞击碰撞摩擦阻力推力拉力压力浮力重力引力斥力磁力电力核力强力弱力";

        return ascii + punctuation + commonHanzi;
    }
}
