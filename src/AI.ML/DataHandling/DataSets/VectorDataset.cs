using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AI.ML.DataHandling.DataSets;

/// <summary>
/// Группа объектов одного класса
/// </summary>
public class GroupeVidData
{
    /// <summary>
    /// Индекс класса(группы)
    /// </summary>
    public int GroupeMark;
    /// <summary>
    /// Мукторы признаков группы
    /// </summary>
    public List<Vector> GroupeFeatures = new List<Vector>();

    /// <summary>
    /// Группа объектов одного класса
    /// </summary>
    public GroupeVidData()
    {

    }

    /// <summary>
    /// Группа объектов одного класса
    /// </summary>
    public GroupeVidData(int gMark, Vector features)
    {
        GroupeMark = gMark;
        GroupeFeatures.Add(features);
    }

    /// <summary>
    /// Вектор средних
    /// </summary>
    public Vector Mean => Statistic.MeanVector(GroupeFeatures);

    /// <summary>
    /// Вектор СКО
    /// </summary>
    public Vector Std => Statistic.EnsembleStd(GroupeFeatures);

    /// <summary>
    /// Возвращет индекс первого вхождения заданной метки класса
    /// </summary>
    /// <param name="lbl">Метка класса</param>
    /// <param name="data">Списо групп</param>
    public static int IndexLbl(IEnumerable<GroupeVidData> data, int lbl)
    {
        int i = 0;
        foreach (var item in data)
        {
            if (lbl == item.GroupeMark) return i;
            i++;
        }
        return -1;
    }
}

/// <summary>
/// Датасет
/// </summary>
[Serializable]
public class VectorDataset : List<VectorDatasetItem>
{
    private readonly Random rnd = new Random(12);
    /// <summary>
    /// Средний вектор
    /// </summary>
    public Vector mean;
    /// <summary>
    /// Дисперсия по выборке
    /// </summary>
    public Vector disp;

    /// <summary>
    /// Загрузка датасета из файла
    /// </summary>
    /// <param name="path">Путь до файла</param>
    public VectorDataset(string path)
    {
        string[] content = File.ReadAllLines(path);
        VectorDatasetItem[] vC = new VectorDatasetItem[content.Length];

        for (int i = 0; i < content.Length; i++)
        {
            string[] strs = content[i].Split(';');

            vC[i] = new VectorDatasetItem(
                Vector.FromStrings(strs[0].Split(' ')),
                Convert.ToInt32(strs[1]));
        }

        AddRange(vC);
    }


    /// <summary>
    /// Датасет
    /// </summary>
    public VectorDataset() { }



    /// <summary>
    /// Датасет
    /// </summary>
    public VectorDataset(int capas) : base(capas)
    { }

    /// <summary>
    /// Случайный представитель датасета
    /// </summary>
    public VectorDatasetItem GetRandomData()
    {
        return this[rnd.Next(Count)];
    }


    /// <summary>
    /// Получение векторов признаков
    /// </summary>
    /// <returns></returns>
    public Vector[] GetFeatures()
    {
        Vector[] vects = new Vector[Count];

        for (int i = 0; i < vects.Length; i++)
        {
            vects[i] = this[i].Features.Clone();
        }

        return vects;
    }

    /// <summary>
    /// Корреляционная матрица признаков
    /// </summary>
    /// <returns>Нормированная кор. матрица</returns>
    public Matrix CorrMatrFeatures()
    {

        var vects = GetFeatures();

        Vector[] vects2 = new Vector[vects[0].Count];

        for (int i = 0; i < vects2.Length; i++)
        {
            vects2[i] = new Vector(vects.Length);


            for (int j = 0; j < vects.Length; j++)
            {
                vects2[i][j] = vects[j][i];
            }
        }


        return Matrix.GetCorrelationMatrixNorm(vects2);
    }


    /// <summary>
    /// Получение вектора дисперсии и среднего вектора
    /// </summary>
    public void DispMeanResult()
    {
        Vector[] vects = GetFeatures();
        mean = Statistic.MeanVector(vects);
        disp = Statistic.EnsembleDispersion(vects);
    }


    /// <summary>
    /// Нормализация датасета
    /// </summary>
    /// <returns>Датасет</returns>
    public VectorDataset ZNormalise(string pathZData = "z_norm", bool isSave = true)
    {

        DispMeanResult();

        disp = disp.Transform(d => (d == 0) ? 1e-109 : d);

        VectorDataset vid = new VectorDataset();
        Vector std = FunctionsForEachElements.Sqrt(disp);

        // Сохранение параметров нормализации
        if (isSave)
        {
            if (!Directory.Exists(pathZData))
                _ = Directory.CreateDirectory(pathZData);

            string stdPath = $"{pathZData}\\std.vect";
            string meanPath = Path.Combine(pathZData, "mean.vect");

            mean.Save(meanPath);
            std.Save(stdPath);
        }

        //Нормализация
        for (int i = 0; i < Count; i++)
        {
            vid.Add(new VectorDatasetItem
                    (
                        (this[i].Features - mean) / std,
                        this[i].ClassMark
                    )
                   );
        }

        return vid;
    }


    /// <summary>
    /// Удаление похожих векторов из разных классов
    /// </summary>
    /// <param name="simCoef">Коэффициент схожести</param>
    public VectorDataset GetDatasetDelSim(double simCoef = 0.9)
    {
        VectorDataset vid = new VectorDataset();
        List<int> simIndex = new List<int>();
        VectorDatasetItem[] vc;

        for (int i = 0; i < Count - 1; i++)
        {
            for (int j = i + 1; j < Count; j++)
            {
                if (this[i].ClassMark != this[j].ClassMark)
                {
                    if (Statistic.CorrelationCoefficient(this[i].Features, this[j].Features) > simCoef)
                    {
                        if (IsNotSerch(simIndex, j))
                        {
                            simIndex.Add(j);
                        }
                    }
                }
            }

        }


        vc = new VectorDatasetItem[Count - simIndex.Count];

        for (int i = 0, k = 0; i < Count; i++)
        {
            if (IsNotSerch(simIndex, i))
            {
                vc[k++] = this[i];
            }
        }

        vid.AddRange(vc);

        return vid;
    }

    private static bool IsNotSerch(List<int> simIndex, int i)
    {
        for (int j = 0; j < simIndex.Count; j++)
        {
            if (i == simIndex[j])
            {
                return false;
            }
        }

        return true;
    }

    //TODO: Ускорить
    /// <summary>
    /// 
    /// </summary>
    /// <param name="path">Путь до файла</param>
    /// <param name="separator"></param>
    /// <returns></returns>
    public static VectorDataset CsvToVid(string path, char separator = ',')
    {
        string[] content = File.ReadAllLines(path);
        VectorDatasetItem[] vC = new VectorDatasetItem[content.Length];
        string[] strs;

        for (int i = 0; i < content.Length; i++)
        {
            strs = content[i].Split(separator);

            vC[i] = new VectorDatasetItem(
                Vector.FromStrings(strs[0].Split(' ')),
                Convert.ToInt32(strs[1]));
        }

        VectorDataset vid = new VectorDataset(content.Length);
        vid.AddRange(vC);
        return vid;
    }

    /// <summary>
    /// 
    /// </summary>
    public static VectorDataset CsvToVid(string path, int len, char separator = ',')
    {
        string[] content = File.ReadAllLines(path);

        len = content.Length > len ? len : content.Length;

        VectorDatasetItem[] vC = new VectorDatasetItem[len];
        string[] strs;

        for (int i = 0; i < len; i++)
        {
            strs = content[i].Split(separator);

            vC[i] = new VectorDatasetItem(
                Vector.FromStrings(strs[0].Split(' ')),
                Convert.ToInt32(strs[1]));
        }

        VectorDataset vid = new VectorDataset(len);
        vid.AddRange(vC);
        return vid;
    }

    /// <summary>
    /// Сохранение датасета
    /// </summary>
    public void Save(string path, char separator = ';')
    {
        StringBuilder stringBuilder = new StringBuilder();

        for (int i = 0; i < Count; i++)
        {
            string features = this[i].Features.ToString();
            features = features.Replace(' ', separator).Replace("[", "").Replace("]", "");
            _ = stringBuilder.Append(features);
            _ = stringBuilder.Append(separator);
            _ = stringBuilder.Append(this[i].ClassMark);
            _ = stringBuilder.Append("\n");
        }

        File.WriteAllText(path, stringBuilder.ToString());
    }


    /// <summary>
    /// Группирует классы вычисляя средний вектор признаков
    /// </summary>
    public VectorDataset GroupMean()
    {
        var data = GetGroupes();
        VectorDataset vid = new VectorDataset(data.Length);

        foreach (var item in data)
            vid.Add(new VectorDatasetItem(item.Mean, item.GroupeMark));
        return vid;
    }

    /// <summary>
    /// Группирует датасет по классам
    /// </summary>
    /// <returns></returns>
    public GroupeVidData[] GetGroupes()
    {
        List<GroupeVidData> vidG = new List<GroupeVidData>();

        foreach (var item in this)
        {
            var ind = GroupeVidData.IndexLbl(vidG, item.ClassMark);
            if (ind == -1) vidG.Add(new GroupeVidData(item.ClassMark, item.Features));
            else vidG[ind].GroupeFeatures.Add(item.Features);
        }

        return vidG.ToArray();
    }

    /// <summary>
    /// Возвращет индекс первого вхождения заданной метки класса
    /// </summary>
    /// <param name="lbl">Метка класса</param>
    public int IndexLbl(int lbl)
    {
        for (int i = 0; i < Count; i++)
            if (lbl == this[i].ClassMark) return i;

        return -1;
    }
}
