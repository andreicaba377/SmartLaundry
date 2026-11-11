using System.Text;

namespace SmartLaundry
{
    // Construieste o lista JSON din elemente deja transformate in text JSON
    public class JArr
    {
        StringBuilder sb = new StringBuilder();
        int count = 0;

        public JArr Add(string raw)
        {
            if (count > 0)
            {
                sb.Append(",");
            }
            sb.Append(raw);
            count++;
            return this;
        }

        public override string ToString()
        {
            return "[" + sb.ToString() + "]";
        }
    }
}
