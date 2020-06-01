def get_block_from(host)
  str = ""
  File.open("(#{host})run-2.log") do |i|
    str = i.read
  end
  dic = []
  str.each_line do |i|
    if i =~ /Now Download *([0-9]+) *from (.+)/
      dic[$1.to_i] = $2
    end
  end
  dic
end

def write_to_file(host)
  a = get_block_from(host)
  File.open("block_from_of_#{host}.txt", "w+") do |file|
    c = 0
    a.each do |i|
      file.write "#{c} => #{i}\n"
      c = c+1
    end
    hash = {}
    a.each do |i|
      if hash[i] == nil
        hash[i] = 1
      else
        hash[i] = hash[i] + 1
      end
    end
    hash.each do |key, value|
      file.write "#{key} => #{value}\n"
    end
  end
end

def gen_graph(a, b)
  peers = {"192.168.1.190" => "A", "192.168.1.132" => "B", "192.168.1.43" => "C"}
  str = "digraph{\n"
  peers.each do |key, value|
    str += "#{value}[label=\"#{key}\"]\n"
  end
  count = 0
  a.each do |i|
    str += "#{peers[i]}->A[label = #{count}]\n"
    count += 1
  end
  b.each do |i|
    str += "#{peers[i]}->B[label =#{count}]\n"
  end
  str += "}"
  File.open("result.dot", "w+") do |i|
    i.write str
  end
end

a = get_block_from("192.168.1.190")
b = get_block_from("192.168.1.132")

write_to_file("192.168.1.190")
write_to_file("192.168.1.132")

puts b.size

a.size.times do |i|
  if a[i] == b[i]
    puts "#{i} => #{a[i]}"
  end
end

gen_graph(a, b)