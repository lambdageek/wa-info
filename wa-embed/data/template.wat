 (module
  (import "imports" "mem" (memory 1))
  (global (export "MonoWebCilVersion") i32 (i32.const 0))
  (global $MonoWebCilDescriptorLength (export "MonoWebCilDescriptorLength") i32 (i32.const 2))
  ;; important to keep descriptor and modules at known data indexes
  (data (; 0 ;) $MonoWebCilDescriptor "\01\02")
  (data (; 1 ;) $MonoWebCilModule0 "MZ\01\02")
  (func (export "MonoWebCilGetDescriptor") (param i32 i32) (result)
    (if (i32.eq (local.get 1) (global.get $MonoWebCilDescriptorLength))
      (memory.init $MonoWebCilDescriptor
        (local.get 0) ;;  dest
        (i32.const 0) ;; src
        (global.get $MonoWebCilDescriptorLength) ;; n
      )
    )
  )
  (func (export "MonoWebCilGetModuleData") (param i32 i32 i32) (result)
    (if (i32.eq (local.get 0) (i32.const 0))
      (memory.init $MonoWebCilModule0 
        (local.get 1) ;; dest
        (i32.const 0) ;; src
        (local.get 2) ;; n
      )
    )
    ;; (if (== moduleIdx) 1) (memory.fill $MonoWebCilModule1Length))
    ;; (if (== moduleIdx) 2) ...)
    ;; ...
  )
)
