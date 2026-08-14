text = 'def fake_single(a)\n  a\nend'
other = "def fake_double(b)\nend"
escaped = "it\\'s a string"

def no_params()
  42
end

def nested_default(a = [1, 2], b = {x: 1})
  a
end
