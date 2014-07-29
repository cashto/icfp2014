fs = require 'fs'
os = require 'os'

debug = process.argv[3] is 'true'

pad = (s, n) ->
    while s.length < n
        s = s + ' '
    return s
    
tokenize = (s) ->
    semiIdx = s.indexOf(';')
    s = s.substring(0, semiIdx) if semiIdx >= 0
    return s.split(/[, ]/).filter((i) -> i.length isnt 0)

lines = 
    fs.readFileSync(process.argv[2], { encoding: 'utf8' })
    
lines = lines
    .split(os.EOL)
    .map((i) -> tokenize(i))
    .filter((i) -> i.length isnt 0)
    
# Pass 1 -- generate labels.
labels = {}
pc = 0

for tokens in lines
    if tokens[0][tokens[0].length - 1] is ':'
        labels["#{tokens[0].substring(0, tokens[0].length - 1)}"] = pc
    else
        pc += 1

# Pass 2 -- emit code.
pc = 0
for tokens in lines
    if tokens[0][tokens[0].length - 1] isnt ':'
        inst = tokens[0] + " " + tokens[1..]
            .map((i) -> 
                if (i[0] is '$')
                    sym = i.substring(1)
                    if labels[sym]?
                        labels[sym]
                    else
                        console.error "ERROR: undefined find symbol '#{sym}'"
                        process.exit()
                else 
                    i)
            .map((i) -> i.toString().toUpperCase())
            .join(', ')
        inst = "#{pad(inst, 40)} ; #{tokens}" if debug
        console.log inst
        pc += 1
