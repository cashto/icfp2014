fs = require 'fs'
os = require 'os'

cadrRegex = /^c[ad]+r$/

debug = process.argv[3] is 'true'

pad = (s, n) ->
    while s.length < n
        s = s + ' '
    return s
    
tokenize = (s) ->
    semiIdx = s.indexOf(';')
    s = s.substring(0, semiIdx) if semiIdx >= 0
    return s.split(' ').filter((i) -> i.length isnt 0)

lines = 
    fs.readFileSync('stdequ.mac',    { encoding: 'utf8' }) +
    fs.readFileSync(process.argv[2], { encoding: 'utf8' }) +
    fs.readFileSync('stdlib.mac',    { encoding: 'utf8' })
    
lines = lines
    .split(os.EOL)
    .map((i) -> tokenize(i))
    .filter((i) -> i.length isnt 0)
    
# Pass 1 -- generate labels.
labels = {}
funcArities = {}
localArities = {}
currentFunc = ''
pc = 0

for tokens in lines
    tokens = [labels[tokens[1]]] if tokens[0] is '!get'
        
    switch tokens[0]
        when '!equ'
            labels[tokens[1]] = tokens[2]
        when '!func'
            currentFunc = tokens[1]
            labels[currentFunc] = pc
            funcArities[currentFunc] = tokens.length - 2
            for token,idx in tokens[2..]
                labels["#{currentFunc}$#{token}"] = idx
        when '!locals'
            localArities[currentFunc] = tokens.length - 1
            for token,idx in tokens[1..]
                labels["#{currentFunc}$#{token}"] = idx
            pc += 3
        when '!call', '!tcall'
            pc += 2
        else
            if tokens[0][tokens[0].length - 1] is ':'
                labels["#{currentFunc}$#{tokens[0].substring(0, tokens[0].length - 1)}"] = pc
            else if cadrRegex.exec(tokens[0])?
                pc += tokens[0].length - 2
            else
                pc += 1

# Pass 2 -- emit code.
currentFunc = ''
pc = 0
for tokens in lines
    tokens = [labels[tokens[1]]] if tokens[0] is '!get'

    switch tokens[0]
        when '!equ'
            undefined
        when '!func'
            currentFunc = tokens[1]
            console.log "" if debug
            console.log "; [#{pad(pc.toString(), 8)}] !func #{currentFunc}" if debug
        when '!locals'
            console.log "LDF #{pc + 3}"
            console.log "AP #{localArities[currentFunc]}"
            console.log "RTN"
            pc += 3
        when '!call', '!tcall'
            op = (if tokens[0] is '!call' then 'AP' else 'TAP')
            if not labels[tokens[1]]?
                console.error "ERROR: undefined find symbol '#{currentFunc}$#{tokens[1]}'"
                process.exit()
            console.log "LDF #{labels[tokens[1]]}"
            inst = "#{op} #{funcArities[tokens[1]]}"
            inst = "#{pad(inst, 40)} ; !call #{tokens[1]}" if debug
            console.log inst
            pc += 2
        else
            if tokens[0][tokens[0].length - 1] is ':'
                undefined
            else if cadrRegex.exec(tokens[0])?
                for ch in tokens[0][1..-2]
                    console.log "C#{ch.toUpperCase()}R"
                pc += tokens[0].length - 2
            else
                inst = tokens
                    .map((i) -> 
                        if (i[0] is '[' and i[i.length - 1] is ']')
                            sym = i.substring(1, i.length - 1)
                            if labels["#{currentFunc}$#{sym}"]?
                                labels["#{currentFunc}$#{sym}"]
                            else if labels[sym]?
                                labels[sym]
                            else
                                console.error "ERROR: undefined find symbol '#{currentFunc}$#{sym}'"
                                process.exit()
                        else 
                            i)
                    .map((i) -> i.toString().toUpperCase())
                    .join(' ')
                inst = "#{pad(inst, 40)} ; #{pc}" if debug
                console.log inst
                pc += 1
