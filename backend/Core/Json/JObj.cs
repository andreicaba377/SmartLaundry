using System.Text;

namespace SmartLaundry
{
    // Construieste un obiect JSON: new JObj().Add("nume", "Ana").Add("varsta", 20)
    //
    // Add returneaza chiar obiectul (this), de aceea apelurile se pot inlantui pe un singur rand.
    // Virgula dintre campuri o punem NOI: inaintea fiecarui camp, mai putin primul (count > 0).
    public class JObj
    {
        StringBuilder sb = new StringBuilder();
        int count = 0;

        public JObj Add(string key, object value)
        {
            return AddRaw(key, J.Val(value));
        }

        // "raw" este deja text JSON (ex. un alt obiect sau o lista)
        public JObj AddRaw(string key, string raw)
        {
            if (count > 0)
            {
                sb.Append(",");
            }
            sb.Append(J.Quote(key));
            sb.Append(":");
            sb.Append(raw);
            count++;
            return this;
        }

        public override string ToString()
        {
            return "{" + sb.ToString() + "}";
        }
    }
}
