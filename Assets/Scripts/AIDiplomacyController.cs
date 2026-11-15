using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 7.2 - Proaktiv Süni İntellekt (AI) Diplomatiya Motoru:
    /// TimeManager-in aylıq Tick döngüsünə bağlanır.
    /// 1. Koalisiya Mexanikası: Əgər Oyunçunun (və ya hər hansı AI-nin) hərbi gücü və ya
    ///    idarə etdiyi əyalət sayı qonşularından 3 dəfə çoxdursa (həddindən artıq güclənibsə),
    ///    digər kiçik dövlətlər aralarında avtomatik Hərbi İttifaq (Koalisiya) quraraq ona qarşı birləşirlər.
    /// 2. Fürsətçilik: Əgər bir dövlət böyük bir müharibədə ordusunun 70%-ni itiribsə (zəifləyibsə),
    ///    münasibəti mənfi olan qonşu AI fürsətdən istifadə edib dərhal ona müharibə elan edir (Ərazi tələbi).
    /// </summary>
    public class AIDiplomacyController : MonoBehaviour
    {
        public static AIDiplomacyController Instance { get; private set; }

        public event Action<int, List<int>> OnCoalitionFormed; // (targetHegemonFactionId, memberFactions)
        public event Action<int, int> OnOpportunisticWarDeclared; // (attackerFactionId, targetFactionId)

        public string LastCoalitionMessage { get; private set; }
        public string LastOpportunisticWarMessage { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// TimeManager / GameManager aylıq Tick döngüsündən çağırılan əsas AI diplomatiya funksiyası.
        /// </summary>
        public void ProcessMonthlyAI(DatabaseManager dbManager, int month, int year)
        {
            if (dbManager == null)
            {
                dbManager = DatabaseManager.Instance != null
                    ? DatabaseManager.Instance
                    : FindAnyObjectByType<DatabaseManager>();
            }

            if (dbManager == null || (!dbManager.IsConnected && !dbManager.InitializeDatabase()))
            {
                return;
            }

            MilitaryAndDiplomacyController milCtrl = MilitaryAndDiplomacyController.Instance != null
                ? MilitaryAndDiplomacyController.Instance
                : FindAnyObjectByType<MilitaryAndDiplomacyController>();

            if (milCtrl == null)
            {
                return;
            }

            dbManager.RunInTransaction(conn =>
            {
                List<Nation> nations = conn.Table<Nation>().ToList();
                List<Province> provinces = conn.Table<Province>().ToList();
                List<Army> armies = conn.Table<Army>().ToList();
                List<Diplomacy> diplomacies = conn.Table<Diplomacy>().ToList();

                if (nations.Count < 2)
                {
                    return;
                }

                // 1. Hər bir dövlətin cari hərbi gücünü və pik hərbi gücünü (PeakMilitaryPower) yeniləyirik
                var militaryPowers = new Dictionary<int, int>();
                var provinceCounts = new Dictionary<int, int>();

                for (int i = 0; i < nations.Count; i++)
                {
                    Nation nat = nations[i];
                    int totalSoldiers = 0;
                    for (int a = 0; a < armies.Count; a++)
                    {
                        if (armies[a].OwnerFactionId == nat.Id)
                        {
                            totalSoldiers += armies[a].TotalSoldiers;
                        }
                    }

                    militaryPowers[nat.Id] = totalSoldiers;

                    int ownedProvinces = 0;
                    for (int p = 0; p < provinces.Count; p++)
                    {
                        if (provinces[p].OwnerFactionId == nat.Id)
                        {
                            ownedProvinces++;
                        }
                    }
                    provinceCounts[nat.Id] = ownedProvinces;

                    // Pik hərbi gücü yadda saxlayırıq
                    if (totalSoldiers > nat.PeakMilitaryPower)
                    {
                        nat.PeakMilitaryPower = totalSoldiers;
                        conn.Update(nat);
                    }
                }

                // 2. Koalisiya Mexanikası (Coalition Mechanics):
                // Əgər bir dövlətin hərbi gücü və ya əyalət sayı digər dövlətlərin orta göstəricisindən 3 dəfə çoxdursa:
                int hegemonId = -1;
                for (int i = 0; i < nations.Count; i++)
                {
                    int fId = nations[i].Id;
                    int power = militaryPowers[fId];
                    int provs = provinceCounts[fId];

                    int otherCount = 0;
                    int otherPowerSum = 0;
                    int otherProvsSum = 0;

                    for (int j = 0; j < nations.Count; j++)
                    {
                        if (nations[j].Id == fId) continue;
                        otherCount++;
                        otherPowerSum += militaryPowers[nations[j].Id];
                        otherProvsSum += provinceCounts[nations[j].Id];
                    }

                    float avgOtherPower = otherCount > 0 ? (float)otherPowerSum / otherCount : 0f;
                    float avgOtherProvs = otherCount > 0 ? (float)otherProvsSum / otherCount : 1f;

                    // Qayda: Hərbi güc 3 dəfə çoxdursa (power >= 3 * avgOtherPower və power >= 3000)
                    // VƏ YA əyalət sayı 3 dəfə çoxdursa (provs >= 3 * avgOtherProvs və provs >= 3):
                    bool isPowerHegemon = (avgOtherPower > 0 && power >= 3f * avgOtherPower) ||
                                          (avgOtherPower == 0 && power >= 3000);
                    bool isProvinceHegemon = (provs >= 3 && provs >= 3f * Mathf.Max(1f, avgOtherProvs));

                    if (isPowerHegemon || isProvinceHegemon)
                    {
                        hegemonId = fId;
                        break;
                    }
                }

                if (hegemonId > 0)
                {
                    var coalitionMembers = new List<int>();
                    for (int i = 0; i < nations.Count; i++)
                    {
                        if (nations[i].Id != hegemonId)
                        {
                            coalitionMembers.Add(nations[i].Id);
                        }
                    }

                    // Kiçik dövlətlər aralarında avtomatik Hərbi İttifaq (Koalisiya) bağlayırlar
                    bool coalitionFormed = false;
                    for (int a = 0; a < coalitionMembers.Count; a++)
                    {
                        for (int b = a + 1; b < coalitionMembers.Count; b++)
                        {
                            int fA = coalitionMembers[a];
                            int fB = coalitionMembers[b];

                            Diplomacy dip = diplomacies.Find(d =>
                                (d.FactionA_Id == fA && d.FactionB_Id == fB) ||
                                (d.FactionA_Id == fB && d.FactionB_Id == fA));

                            if (dip == null)
                            {
                                dip = new Diplomacy
                                {
                                    FactionA_Id = fA,
                                    FactionB_Id = fB,
                                    RelationScore = 80,
                                    TreatyType = "Hərbi İttifaq (Koalisiya)",
                                    TreatyDuration = 120
                                };
                                conn.Insert(dip);
                                diplomacies.Add(dip);
                                coalitionFormed = true;
                            }
                            else if (!dip.IsAlliance)
                            {
                                dip.TreatyType = "Hərbi İttifaq (Koalisiya)";
                                dip.RelationScore = Mathf.Max(80, dip.RelationScore);
                                dip.TreatyDuration = 120;
                                conn.Update(dip);
                                coalitionFormed = true;
                            }
                        }

                        // Koalisiya üzvlərinin Hegemon dövlətlə münasibəti düşmənə çevrilir (RelationScore <= -60)
                        int memId = coalitionMembers[a];
                        Diplomacy hegemonDip = diplomacies.Find(d =>
                            (d.FactionA_Id == memId && d.FactionB_Id == hegemonId) ||
                            (d.FactionA_Id == hegemonId && d.FactionB_Id == memId));

                        if (hegemonDip != null)
                        {
                            if (hegemonDip.RelationScore > -60)
                            {
                                hegemonDip.RelationScore = -75;
                                conn.Update(hegemonDip);
                            }
                        }
                        else
                        {
                            hegemonDip = new Diplomacy
                            {
                                FactionA_Id = memId,
                                FactionB_Id = hegemonId,
                                RelationScore = -75,
                                TreatyType = "Düşmən (Koalisiya Hədəfi)",
                                TreatyDuration = 60
                            };
                            conn.Insert(hegemonDip);
                            diplomacies.Add(hegemonDip);
                        }
                    }

                    if (coalitionFormed)
                    {
                        Nation hegemonNation = nations.Find(n => n.Id == hegemonId);
                        string hegemonName = hegemonNation != null ? hegemonNation.Name : $"Fraksiya #{hegemonId}";
                        LastCoalitionMessage = $"KOALİSİYA QURULDU! '{hegemonName}' həddindən artıq gücləndiyi üçün (Güc/Əyalət 3x çox) digər dövlətlər ona qarşı Hərbi İttifaq qurdular!";
                        Debug.LogWarning($"[AIDiplomacyController] {LastCoalitionMessage}");
                        OnCoalitionFormed?.Invoke(hegemonId, coalitionMembers);
                    }
                }

                // 3. Fürsətçilik (Opportunism):
                // Əgər bir dövlət böyük bir müharibədə ordusunun 70%-ni itiribsə (zəifləyibsə):
                for (int i = 0; i < nations.Count; i++)
                {
                    Nation targetNat = nations[i];
                    int curPower = militaryPowers[targetNat.Id];
                    int peakPower = targetNat.PeakMilitaryPower;

                    // Ən azı 500 əsgəri olmuş və ordusunun 70%-dən çoxunu itirmiş (qalan güc <= 30%) dövlət:
                    if (peakPower >= 500 && curPower <= (peakPower * 0.30f))
                    {
                        // Münasibəti mənfi olan qonşu AI-lər fürsətdən istifadə edir
                        for (int j = 0; j < nations.Count; j++)
                        {
                            if (nations[j].Id == targetNat.Id) continue;
                            int aiId = nations[j].Id;

                            Diplomacy dip = diplomacies.Find(d =>
                                (d.FactionA_Id == aiId && d.FactionB_Id == targetNat.Id) ||
                                (d.FactionA_Id == targetNat.Id && d.FactionB_Id == aiId));

                            // Münasibət mənfidirsə (RelationScore < 0) və hazırda müharibə getmirsə:
                            if (dip != null && dip.RelationScore < 0 && dip.TreatyType != "Müharibə")
                            {
                                // AI Fürsətdən istifadə edərək dərhal müharibə elan edir!
                                dip.TreatyType = "Müharibə";
                                dip.RelationScore = -100;
                                dip.CeasefireMonthsLeft = 0;
                                dip.NonAggressionMonthsLeft = 0;
                                dip.TreatyDuration = 24;
                                conn.Update(dip);

                                LastOpportunisticWarMessage = $"FÜRSƏTÇİ HÜCUM! Fraksiya #{aiId} ({nations[j].Name}) ordusunun 70%-ni itirmiş zəifləmiş Fraksiya #{targetNat.Id}-ə ({targetNat.Name}) ərazi tələbi ilə müharibə elan etdi!";
                                Debug.LogWarning($"[AIDiplomacyController] {LastOpportunisticWarMessage}");
                                OnOpportunisticWarDeclared?.Invoke(aiId, targetNat.Id);
                                break;
                            }
                        }
                    }
                }
            });
        }
    }
}
