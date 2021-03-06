﻿// Case-Insensitive and Concatenating OptionSet
using System;
using System.Collections.Generic;


namespace Mono.Options
{
  class OptionSet_icase : OptionSet
  {
    protected override void InsertItem(int index, Option item)
    {
      if (item.Prototype.ToLower() != item.Prototype)
        throw new ArgumentException("prototypes must be lower-case!");
      base.InsertItem(index, item);
    }

    protected override OptionContext CreateOptionContext()
    {
      return new OptionContext(this);
    }

    protected override bool Parse(string option, OptionContext c)
    {
      string f, n, s, v;
      bool haveParts = GetOptionParts(option, out f, out n, out s, out v);
      Option nextOption = null;
      string newOption = option;

      if (haveParts)
      {
        string nl = n.ToLower();
        nextOption = Contains(nl) ? this[nl] : null;
        newOption = f + n.ToLower() + (v != null ? s + v : "");
      }

      if (c.Option != null)
      {
        // Prevent --a --b
        if (c.Option != null && haveParts)
        {
          if (nextOption == null)
          {
            // ignore
          }
          else
            throw new OptionException(
                string.Format("Found option `{0}' as value for option `{1}'.",
                    option, c.OptionName), c.OptionName);
        }

        // have a option w/ required value; try to concat values.
        if (AppendValue(option, c))
        {
          if (!option.EndsWith("\\") &&
                  c.Option.MaxValueCount == c.OptionValues.Count)
          {
            c.Option.Invoke(c);
          }
          return true;
        }
        else
          base.Parse(newOption, c);
      }

      if (!haveParts || v == null)
      {
        // Not an option; let base handle as a non-option argument.
        return base.Parse(newOption, c);
      }

      if (nextOption.OptionValueType != OptionValueType.None &&
              v.EndsWith("\\"))
      {
        c.Option = nextOption;
        c.OptionValues.Add(v);
        c.OptionName = f + n;
        return true;
      }

      return base.Parse(newOption, c);
    }

    private bool AppendValue(string value, OptionContext c)
    {
      bool added = false;
      string[] seps = c.Option.GetValueSeparators();
      foreach (var o in seps.Length != 0
              ? value.Split(seps, StringSplitOptions.None)
              : new string[] { value })
      {
        int idx = c.OptionValues.Count - 1;
        if (idx == -1 || !c.OptionValues[idx].EndsWith("\\"))
        {
          c.OptionValues.Add(o);
          added = true;
        }
        else
        {
          c.OptionValues[idx] += value;
          added = true;
        }
      }
      return added;
    }
  }

}