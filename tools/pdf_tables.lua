-- Pandoc filter for tools/make_pdf.py: gives every table column a width (so the PDF wraps the
-- cells instead of running off the page), in proportion to the text of the column.
local function len(cell)
  return #pandoc.utils.stringify(cell.contents or cell)
end

function Table(tbl)
  local n = #tbl.colspecs
  if n == 0 then return nil end
  local w = {}
  for i = 1, n do w[i] = 4 end
  local function scan(rows)
    for _, row in ipairs(rows) do
      for i, cell in ipairs(row.cells) do
        if w[i] then
          local l = len(cell)
          if l > 60 then l = 60 + (l - 60) / 3 end   -- a long text takes room, but not all of it
          if l > w[i] then w[i] = l end
        end
      end
    end
  end
  scan(tbl.head.rows)
  for _, body in ipairs(tbl.bodies) do scan(body.body) end
  local total = 0
  for i = 1, n do total = total + w[i] end
  for i = 1, n do
    tbl.colspecs[i] = { tbl.colspecs[i][1], w[i] / total * 0.96 }
  end
  return tbl
end
