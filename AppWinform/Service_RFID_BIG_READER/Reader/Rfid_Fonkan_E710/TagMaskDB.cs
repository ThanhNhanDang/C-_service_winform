using Reader;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Service_RFID_BIG_READER.Reader.Rfid_Fonkan_E710
{
    public class TagMaskBindingList : SortableBindingList<TagMask>
    {
        protected override Comparison<TagMask> GetComparer(PropertyDescriptor prop)
        {
            Comparison<TagMask> comparer = null;
            switch (prop.Name)
            {
                case "MaskID":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.MaskID - b.MaskID);
                    });
                    break;
                case "Quantity":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.Quantity - b.Quantity);
                    });
                    break;
                case "Target":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.Target - b.Target);
                    });
                    break;
                case "Action":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.Action - b.Action);
                    });
                    break;
                case "ActionStr":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return String.Compare(a.ActionStr, b.ActionStr);
                    });
                    break;
                case "Bank":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.Bank - b.Bank);
                    });
                    break;
                case "StartAddrHexStr":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return String.Compare(a.StartAddrHexStr, b.StartAddrHexStr);
                    });
                    break;
                case "StartAddr":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.StartAddr - b.StartAddr);
                    });
                    break;
                case "MaskBitLenHexStr":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return String.Compare(a.MaskBitLenHexStr, b.MaskBitLenHexStr);
                    });
                    break;
                case "MaskBitLen":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.MaskBitLen - b.MaskBitLen);
                    });
                    break;
                case "Mask":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return String.Compare(a.Mask, b.Mask);
                    });
                    break;
                case "Truncate":
                    comparer = new Comparison<TagMask>(delegate (TagMask a, TagMask b)
                    {
                        return (int)(a.Truncate - b.Truncate);
                    });
                    break;
            }
            return comparer;
        }
    }

    public class TagMaskDB
    {
        private TagMaskBindingList _tagList = new TagMaskBindingList();

        /// <summary>
        /// EPC index into tag list
        /// </summary>
        private Dictionary<string, TagMask> TagMaskIndex = new Dictionary<string, TagMask>();

        public BindingList<TagMask> TagList
        {
            get { return _tagList; }
        }

        public void Clear()
        {
            TagMaskIndex.Clear();
            _tagList.Clear();
            // Clear doesn't fire notifications on its own
            _tagList.ResetBindings();
        }

        public void Add(TagMask addData)
        {
            lock (new Object())
            {
                string key = null;
                key = string.Format("mask_id{0}", addData.MaskID);

                if (!TagMaskIndex.ContainsKey(key))
                {
                    _tagList.Add(addData);
                    TagMaskIndex.Add(key, addData);
                }
                else
                {
                    TagMaskIndex[key].Update(addData);
                }
            }
        }
    }
}