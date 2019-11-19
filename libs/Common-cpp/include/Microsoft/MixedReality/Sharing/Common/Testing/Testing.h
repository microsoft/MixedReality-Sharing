// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include <cassert>
#include <cstdint>
#include <functional>
#include <thread>
#include <vector>

namespace Microsoft::MixedReality::Sharing::Testing {

// Executes the provided function in parallel on all available hardware threads.
// The range [0, ids_count) will be split between multiple threads, each
// receiving a portion in form of [begin_id, end_id).
void RunInParallel(uint64_t ids_count,
                   std::function<void(uint64_t, uint64_t)> func) {
  auto threads_count = std::thread::hardware_concurrency();
  // In case the runs won't be split equally, we want to execute larger ones
  // first. For example, splitting 6 runs between 4 threads will produce
  // the following ranges: [0, 2), [2, 4), [4, 5), [5, 6).
  uint64_t min_ids_per_thread = 1;
  uint64_t threads_count_with_extra_id = 0;
  if (threads_count >= ids_count) {
    threads_count = static_cast<unsigned int>(ids_count);
  } else {
    min_ids_per_thread = ids_count / threads_count;
    threads_count_with_extra_id = ids_count % threads_count;
  }
  std::vector<std::thread> threads;
  threads.reserve(threads_count);

  uint64_t begin_id = 0;
  for (unsigned int i = 0; i < threads_count; ++i) {
    uint64_t end_id = begin_id + min_ids_per_thread;
    if (i < threads_count_with_extra_id)
      ++end_id;
    assert(begin_id != end_id && end_id <= ids_count);
    threads.emplace_back([&] { func(begin_id, end_id); });
    begin_id = end_id;
  }
  assert(begin_id == ids_count);
  for (auto& thread : threads)
    thread.join();
}

}  // namespace Microsoft::MixedReality::Sharing::Testing
