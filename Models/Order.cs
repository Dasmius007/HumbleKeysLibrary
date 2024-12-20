using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace HumbleKeys.Models
{
    public class Order
    {
        public class Product
        {
            public string category;
            public string machine_name;
            public string human_name;
            public string choice_url;
            public bool is_subs_v2_product;
            public bool is_subs_v3_product;
        }

        public class SubProduct
        {
            public class Download
            {
                public class DownloadStruct
                {
                    public class Url
                    {
                        public string web;
                        public string bittorrent;
                    }

                    public string human_size;
                    public string name;
                    public string sha1;
                    public ulong file_size;
                    public string md5;
                    public Url url;
                }

                public List<DownloadStruct> download_struct;
                public string machine_name;
                public string platform;
                public bool android_app_only;
            }

            public string machine_name;
            public string url;
            public List<Download> downloads;
            public string human_name;
            public string icon;
            public string library_family_name;
        }

        public class TpkdDict
        {
            public class Tpk
            {
                public string machine_name;
                public string gamekey;
                public string key_type;
                public bool visible;
                public string instructions_html;
                public string key_type_human_name;
                public string human_name;
                public string @class;
                public string library_family_name;
                public string steam_app_id;
                public bool is_expired;
                public bool sold_out;
                [JsonProperty("expiration_date|datetime")]
                public DateTime expiration_date;
                public int num_days_until_expired;
                public Newtonsoft.Json.Linq.JToken redeemed_key_val;
                public bool is_virtual = false;
            }

            public List<Tpk> all_tpks;
        }

        public string gamekey;
        public string uid;
        public Product product;
        public List<SubProduct> subproducts;
        public TpkdDict tpkd_dict;
        public List<string> path_ids;
        // v3 seems to mean how many of the bundle has been selected
        // v2 seems to mean number of games available to be redeemed
        public int total_choices;
        // v3 always 0?
        // v2 total_choices - number of games redeemed
        public int choices_remaining;
    }
}
