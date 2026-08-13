def add(a, b)
  a + b
end

def greet(name, title = nil)
  [title, name].compact.join(" ")
end

def with_keywords(required:, optional: "x")
  "#{required}-#{optional}"
end

def _hidden(value)
  value
end

def with_splats(first, *rest, **options, &block)
  [first, rest, options, block]
end
