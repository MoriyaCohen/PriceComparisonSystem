using PriceComparison.Application.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PriceComparison.Application.Services
{
    public class PriceSyncService
    {
        public void GenerateSyncedFile(string priceXmlPath, string promoXmlPath, string outputFolder)
        {
            // 1. טעינת קובץ המחירים המקורי
            var xDocPrice = XDocument.Load(priceXmlPath);
            Dictionary<string, dynamic> promoDict = new Dictionary<string, dynamic>();

            // 2. טעינת מבצעים (אם הקובץ קיים)
            if (File.Exists(promoXmlPath))
            {
                var xDocPromo = XDocument.Load(promoXmlPath);
                var promoItems = xDocPromo.Descendants("Promotion").ToList();

                foreach (var p in promoItems)
                {
                    // מציאת כל הפריטים במבצע
                    var itemsInPromo = p.Descendants("Item")
                                        .Select(i => i.Element("ItemCode")?.Value)
                                        .Where(x => x != null)
                                        .ToList();

                    decimal discountPrice = decimal.TryParse(p.Element("DiscountedPrice")?.Value, out var dp) ? dp : 0;
                    string desc = p.Element("PromotionDescription")?.Value ?? "";
                    string startDate = p.Element("PromotionStartDate")?.Value ?? ""; // תאריך התחלה
                    string endDate = p.Element("PromotionEndDate")?.Value ?? "";     // תאריך סיום

                    // זיהוי מועדון
                    bool isClub = desc.Contains("מועדון");
                    var restrictions = p.Element("AdditionalRestrictions");
                    if (restrictions != null && restrictions.Descendants("ClubId").Any()) isClub = true;

                    // זיהוי כמות
                    bool isQuantity = false;
                    if (int.TryParse(p.Element("MinQty")?.Value, out int minQty) && minQty > 1) isQuantity = true;

                    foreach (var itemCode in itemsInPromo)
                    {
                        if (!promoDict.ContainsKey(itemCode))
                        {
                            promoDict.Add(itemCode, new
                            {
                                Price = discountPrice,
                                Desc = desc,
                                Start = startDate,
                                End = endDate,
                                IsClub = isClub,
                                IsQuantity = isQuantity
                            });
                        }
                    }
                }
            }

            // 3. עדכון קובץ המחירים
            var items = xDocPrice.Descendants("Item");
            foreach (var item in items)
            {
                var code = item.Element("ItemCode")?.Value;

                // *** מחיקת תגיות ישנות (מונע כפילויות בריצות חוזרות) ***
                item.Elements("HasPromo").Remove();
                item.Elements("PromoPrice").Remove();
                item.Elements("PromoDescription").Remove();
                item.Elements("PromoStartDate").Remove();
                item.Elements("PromoEndDate").Remove();
                item.Elements("IsClub").Remove();
                item.Elements("IsQuantity").Remove();

                if (code != null && promoDict.TryGetValue(code, out var promo))
                {
                    // הוספת תגיות חדשות אם יש מבצע
                    item.Add(new XElement("HasPromo", "true"));
                    item.Add(new XElement("PromoPrice", promo.Price));
                    item.Add(new XElement("PromoDescription", promo.Desc));
                    item.Add(new XElement("PromoStartDate", promo.Start));
                    item.Add(new XElement("PromoEndDate", promo.End));
                    item.Add(new XElement("IsClub", promo.IsClub ? "true" : "false"));
                    item.Add(new XElement("IsQuantity", promo.IsQuantity ? "true" : "false"));
                }
                else
                {
                    // סימון שאין מבצע
                    item.Add(new XElement("HasPromo", "false"));
                }
            }

            // 4. שמירה בטוחה ויפה
            // בונה את הנתיב המלא לתיקיית היעד (PriceDir)
            string fileName = Path.GetFileName(priceXmlPath);
            string fullPath = Path.Combine(outputFolder, fileName);

            // SaveOptions.None שומר על רווחים והזחות (XML קריא ומסודר)
            xDocPrice.Save(fullPath, SaveOptions.None);
        }
    }
}