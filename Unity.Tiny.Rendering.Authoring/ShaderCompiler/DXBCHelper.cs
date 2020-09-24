using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Assertions;

namespace Unity.Tiny.ShaderCompiler
{
    /// <remarks>
    /// Referenced from https://github.com/bkaradzic/bgfx/blob/master/src/shader_dxbc.cpp
    /// </remarks>
    static class DXBCHelper
    {
        #region OpCodes

        enum DxbcOpcode
        {
            ADD,
            AND,
            BREAK,
            BREAKC,
            CALL,
            CALLC,
            CASE,
            CONTINUE,
            CONTINUEC,
            CUT,
            DEFAULT,
            DERIV_RTX,
            DERIV_RTY,
            DISCARD,
            DIV,
            DP2,
            DP3,
            DP4,
            ELSE,
            EMIT,
            EMITTHENCUT,
            ENDIF,
            ENDLOOP,
            ENDSWITCH,
            EQ,
            EXP,
            FRC,
            FTOI,
            FTOU,
            GE,
            IADD,
            IF,
            IEQ,
            IGE,
            ILT,
            IMAD,
            IMAX,
            IMIN,
            IMUL,
            INE,
            INEG,
            ISHL,
            ISHR,
            ITOF,
            LABEL,
            LD,
            LD_MS,
            LOG,
            LOOP,
            LT,
            MAD,
            MIN,
            MAX,
            CUSTOMDATA,
            MOV,
            MOVC,
            MUL,
            NE,
            NOP,
            NOT,
            OR,
            RESINFO,
            RET,
            RETC,
            ROUND_NE,
            ROUND_NI,
            ROUND_PI,
            ROUND_Z,
            RSQ,
            SAMPLE,
            SAMPLE_C,
            SAMPLE_C_LZ,
            SAMPLE_L,
            SAMPLE_D,
            SAMPLE_B,
            SQRT,
            SWITCH,
            SINCOS,
            UDIV,
            ULT,
            UGE,
            UMUL,
            UMAD,
            UMAX,
            UMIN,
            USHR,
            UTOF,
            XOR,
            DCL_RESOURCE,
            DCL_CONSTANT_BUFFER,
            DCL_SAMPLER,
            DCL_INDEX_RANGE,
            DCL_GS_OUTPUT_PRIMITIVE_TOPOLOGY,
            DCL_GS_INPUT_PRIMITIVE,
            DCL_MAX_OUTPUT_VERTEX_COUNT,
            DCL_INPUT,
            DCL_INPUT_SGV,
            DCL_INPUT_SIV,
            DCL_INPUT_PS,
            DCL_INPUT_PS_SGV,
            DCL_INPUT_PS_SIV,
            DCL_OUTPUT,
            DCL_OUTPUT_SGV,
            DCL_OUTPUT_SIV,
            DCL_TEMPS,
            DCL_INDEXABLE_TEMP,
            DCL_GLOBAL_FLAGS,

            UnknownD3D10,
            LOD,
            GATHER4,
            SAMPLE_POS,
            SAMPLE_INFO,

            UnknownD3D10_1,
            HS_DECLS,
            HS_CONTROL_POINT_PHASE,
            HS_FORK_PHASE,
            HS_JOIN_PHASE,
            EMIT_STREAM,
            CUT_STREAM,
            EMITTHENCUT_STREAM,
            INTERFACE_CALL,
            BUFINFO,
            DERIV_RTX_COARSE,
            DERIV_RTX_FINE,
            DERIV_RTY_COARSE,
            DERIV_RTY_FINE,
            GATHER4_C,
            GATHER4_PO,
            GATHER4_PO_C,
            RCP,
            F32TOF16,
            F16TOF32,
            UADDC,
            USUBB,
            COUNTBITS,
            FIRSTBIT_HI,
            FIRSTBIT_LO,
            FIRSTBIT_SHI,
            UBFE,
            IBFE,
            BFI,
            BFREV,
            SWAPC,
            DCL_STREAM,
            DCL_FUNCTION_BODY,
            DCL_FUNCTION_TABLE,
            DCL_INTERFACE,
            DCL_INPUT_CONTROL_POINT_COUNT,
            DCL_OUTPUT_CONTROL_POINT_COUNT,
            DCL_TESS_DOMAIN,
            DCL_TESS_PARTITIONING,
            DCL_TESS_OUTPUT_PRIMITIVE,
            DCL_HS_MAX_TESSFACTOR,
            DCL_HS_FORK_PHASE_INSTANCE_COUNT,
            DCL_HS_JOIN_PHASE_INSTANCE_COUNT,
            DCL_THREAD_GROUP,
            DCL_UNORDERED_ACCESS_VIEW_TYPED,
            DCL_UNORDERED_ACCESS_VIEW_RAW,
            DCL_UNORDERED_ACCESS_VIEW_STRUCTURED,
            DCL_THREAD_GROUP_SHARED_MEMORY_RAW,
            DCL_THREAD_GROUP_SHARED_MEMORY_STRUCTURED,
            DCL_RESOURCE_RAW,
            DCL_RESOURCE_STRUCTURED,
            LD_UAV_TYPED,
            STORE_UAV_TYPED,
            LD_RAW,
            STORE_RAW,
            LD_STRUCTURED,
            STORE_STRUCTURED,
            ATOMIC_AND,
            ATOMIC_OR,
            ATOMIC_XOR,
            ATOMIC_CMP_STORE,
            ATOMIC_IADD,
            ATOMIC_IMAX,
            ATOMIC_IMIN,
            ATOMIC_UMAX,
            ATOMIC_UMIN,
            IMM_ATOMIC_ALLOC,
            IMM_ATOMIC_CONSUME,
            IMM_ATOMIC_IADD,
            IMM_ATOMIC_AND,
            IMM_ATOMIC_OR,
            IMM_ATOMIC_XOR,
            IMM_ATOMIC_EXCH,
            IMM_ATOMIC_CMP_EXCH,
            IMM_ATOMIC_IMAX,
            IMM_ATOMIC_IMIN,
            IMM_ATOMIC_UMAX,
            IMM_ATOMIC_UMIN,
            SYNC,
            DADD,
            DMAX,
            DMIN,
            DMUL,
            DEQ,
            DGE,
            DLT,
            DNE,
            DMOV,
            DMOVC,
            DTOF,
            FTOD,
            EVAL_SNAPPED,
            EVAL_SAMPLE_INDEX,
            EVAL_CENTROID,
            DCL_GS_INSTANCE_COUNT,
            ABORT,
            DEBUG_BREAK,

            UnknownD3D11,
            DDIV,
            DFMA,
            DRCP,
            MSAD,
            DTOI,
            DTOU,
            ITOD,
            UTOD,

            Count
        }

        struct DxbcOpcodeInfo
        {
            internal byte numOperands;
            internal byte numValues;
        };

        static DxbcOpcodeInfo[] s_DxbcOpcodeInfo =
        {
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ADD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // AND
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // BREAK
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // BREAKC
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // CALL
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // CALLC
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // CASE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // CONTINUE
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // CONTINUEC
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // CUT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DEFAULT
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTX
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTY
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DISCARD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DIV
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DP2
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DP3
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DP4
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ELSE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // EMIT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // EMITTHENCUT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ENDIF
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ENDLOOP
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ENDSWITCH
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // EQ
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // EXP
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // FRC
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // FTOI
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // FTOU
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // GE
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // IADD
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // IF
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // IEQ
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // IGE
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ILT
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // IMAD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // IMAX
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // IMIN
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // IMUL
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // INE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // INEG
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ISHL
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ISHR
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // ITOF
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // LABEL
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // LD
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // LD_MS
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // LOG
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // LOOP
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // LT
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // MAD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // MIN
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // MAX
            new DxbcOpcodeInfo { numOperands = 0, numValues = 1 }, // CUSTOMDATA
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // MOV
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // MOVC
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // MUL
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // NE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // NOP
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // NOT
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // OR
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // RESINFO
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // RET
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // RETC
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // ROUND_NE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // ROUND_NI
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // ROUND_PI
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // ROUND_Z
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // RSQ
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // SAMPLE
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // SAMPLE_C
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // SAMPLE_C_LZ
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // SAMPLE_L
            new DxbcOpcodeInfo { numOperands = 6, numValues = 0 }, // SAMPLE_D
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // SAMPLE_B
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // SQRT
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // SWITCH
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // SINCOS
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // UDIV
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ULT
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // UGE
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // UMUL
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // UMAD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // UMAX
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // UMIN
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // USHR
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // UTOF
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // XOR
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_RESOURCE
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_CONSTANT_BUFFER
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_SAMPLER
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_INDEX_RANGE
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_GS_OUTPUT_PRIMITIVE_TOPOLOGY
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_GS_INPUT_PRIMITIVE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 1 }, // DCL_MAX_OUTPUT_VERTEX_COUNT
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_INPUT
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_INPUT_SGV
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_INPUT_SIV
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_INPUT_PS
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_INPUT_PS_SGV
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_INPUT_PS_SIV
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_OUTPUT
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_OUTPUT_SGV
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_OUTPUT_SIV
            new DxbcOpcodeInfo { numOperands = 0, numValues = 1 }, // DCL_TEMPS
            new DxbcOpcodeInfo { numOperands = 0, numValues = 3 }, // DCL_INDEXABLE_TEMP
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_GLOBAL_FLAGS

            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // InstrD3D10
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // LOD
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // GATHER4
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // SAMPLE_POS
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // SAMPLE_INFO

            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // InstrD3D10_1
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // HS_DECLS
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // HS_CONTROL_POINT_PHASE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // HS_FORK_PHASE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // HS_JOIN_PHASE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // EMIT_STREAM
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // CUT_STREAM
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // EMITTHENCUT_STREAM
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // INTERFACE_CALL
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // BUFINFO
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTX_COARSE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTX_FINE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTY_COARSE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DERIV_RTY_FINE
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // GATHER4_C
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // GATHER4_PO
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // GATHER4_PO_C
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // RCP
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // F32TOF16
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // F16TOF32
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // UADDC
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // USUBB
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // COUNTBITS
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // FIRSTBIT_HI
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // FIRSTBIT_LO
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // FIRSTBIT_SHI
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // UBFE
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // IBFE
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // BFI
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // BFREV
            new DxbcOpcodeInfo { numOperands = 5, numValues = 0 }, // SWAPC
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_STREAM
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_FUNCTION_BODY
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_FUNCTION_TABLE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_INTERFACE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_INPUT_CONTROL_POINT_COUNT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_OUTPUT_CONTROL_POINT_COUNT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_TESS_DOMAIN
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_TESS_PARTITIONING
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_TESS_OUTPUT_PRIMITIVE
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_HS_MAX_TESSFACTOR
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_HS_FORK_PHASE_INSTANCE_COUNT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DCL_HS_JOIN_PHASE_INSTANCE_COUNT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 3 }, // DCL_THREAD_GROUP
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_UNORDERED_ACCESS_VIEW_TYPED
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_UNORDERED_ACCESS_VIEW_RAW
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_UNORDERED_ACCESS_VIEW_STRUCTURED
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_THREAD_GROUP_SHARED_MEMORY_RAW
            new DxbcOpcodeInfo { numOperands = 1, numValues = 2 }, // DCL_THREAD_GROUP_SHARED_MEMORY_STRUCTURED
            new DxbcOpcodeInfo { numOperands = 1, numValues = 0 }, // DCL_RESOURCE_RAW
            new DxbcOpcodeInfo { numOperands = 1, numValues = 1 }, // DCL_RESOURCE_STRUCTURED
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // LD_UAV_TYPED
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // STORE_UAV_TYPED
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // LD_RAW
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // STORE_RAW
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // LD_STRUCTURED
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // STORE_STRUCTURED
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_AND
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_OR
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_XOR
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_CMP_STORE
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_IADD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_IMAX
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_IMIN
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_UMAX
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // ATOMIC_UMIN
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // IMM_ATOMIC_ALLOC
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // IMM_ATOMIC_CONSUME
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_IADD
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_AND
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_OR
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_XOR
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_EXCH
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_CMP_EXCH
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_IMAX
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_IMIN
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_UMAX
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // IMM_ATOMIC_UMIN
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // SYNC
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DADD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DMAX
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DMIN
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DMUL
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DEQ
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DGE
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DLT
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // DNE
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // DMOV
            new DxbcOpcodeInfo { numOperands = 4, numValues = 0 }, // DMOVC
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DTOF
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // FTOD
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // EVAL_SNAPPED
            new DxbcOpcodeInfo { numOperands = 3, numValues = 0 }, // EVAL_SAMPLE_INDEX
            new DxbcOpcodeInfo { numOperands = 2, numValues = 0 }, // EVAL_CENTROID
            new DxbcOpcodeInfo { numOperands = 0, numValues = 1 }, // DCL_GS_INSTANCE_COUNT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ABORT
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DEBUG_BREAK

            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // InstrD3D11
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DDIV
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DFMA
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DRCP
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // MSAD
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DTOI
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // DTOU
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // ITOD
            new DxbcOpcodeInfo { numOperands = 0, numValues = 0 }, // UTOD
        };

        enum DxbcOperandType
        {
            Temp,
            Input,
            Output,
            TempArray,
            Imm32,
            Imm64,
            Sampler,
            Resource,
            ConstantBuffer,
            ImmConstantBuffer,
            Label,
            PrimitiveID,
            OutputDepth,
            Null,
            Rasterizer,
            CoverageMask,
            Stream,
            FunctionBody,
            FunctionTable,
            Interface,
            FunctionInput,
            FunctionOutput,
            OutputControlPointId,
            InputForkInstanceId,
            InputJoinInstanceId,
            InputControlPoint,
            OutputControlPoint,
            InputPatchConstant,
            InputDomainPoint,
            ThisPointer,
            UnorderedAccessView,
            ThreadGroupSharedMemory,
            InputThreadId,
            InputThreadGroupId,
            InputThreadIdInGroup,
            InputCoverageMask,
            InputThreadIdInGroupFlattened,
            InputGsInstanceId,
            OutputDepthGreaterEqual,
            OutputDepthLessEqual,
            CycleCounter,

            Count
        }

        enum DxbcResourceDim
        {
            // D3D_SRV_DIMENSION
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ff728736%28v=vs.85%29.aspx
            // mesa/src/gallium/state_trackers/d3d1x/d3d1xshader/defs/targets.txt
            Unknown,
            Buffer,
            Texture1D,
            Texture2D,
            Texture2DMS,
            Texture3D,
            TextureCube,
            Texture1DArray,
            Texture2DArray,
            Texture2DMSArray,
            TextureCubearray,
            RawBuffer,
            StructuredBuffer,

            Count
        }

        enum DxbcInterpolation
        {
            Unknown,
            Constant,
            Linear,
            LinearCentroid,
            LinearNoPerspective,
            LinearNoPerspectiveCentroid,
            LinearSample,
            LinearNoPerspectiveSample,

            Count
        }

        enum DxbcResourceReturnType
        {
            Unorm,
            Snorm,
            Sint,
            Uint,
            Float,
            Mixed,
            Double,
            Continued,
            Unused,

            Count
        }

        enum DxbcOperandAddrMode
        {
            Imm32,
            Imm64,
            Reg,
            RegImm32,
            RegImm64,

            Count
        }

        enum DxbcCustomDataClass
        {
            Comment,
            DebugInfo,
            Opaque,
            ImmConstantBuffer,
            ShaderMessage,
            ClipPlaneConstantMappingsForDx9,

            Count
        }

        enum DxbcOperandMode
        {
            Mask,
            Swizzle,
            Scalar,

            Count
        }

        enum DxbcOperandModifier
        {
            None,
            Neg,
            Abs,
            AbsNeg,

            Count
        }

        enum DxbcBuiltin
        {
            // D3D_NAME
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ff728724%28v=vs.85%29.aspx
            // mesa/src/gallium/state_trackers/d3d1x/d3d1xshader/defs/svs.txt
            Undefined,
            Position,
            ClipDistance,
            CullDistance,
            RenderTargetArrayIndex,
            ViewportArrayIndex,
            VertexId,
            PrimitiveId,
            InstanceId,
            IsFrontFace,
            SampleIndex,
            FinalQuadUEq0EdgeTessFactor,
            FinalQuadVEq0EdgeTessFactor,
            FinalQuadUEq1EdgeTessFactor,
            FinalQuadVEq1EdgeTessFactor,
            FinalQuadUInsideTessFactor,
            FinalQuadVInsideTessFactor,
            FinalTriUEq0EdgeTessFactor,
            FinalTriVEq0EdgeTessFactor,
            FinalTriWEq0EdgeTessFactor,
            FinalTriInsideTessFactor,
            FinalLineDetailTessFactor,
            FinalLineDensityTessFactor,
            Target = 64,
            Depth,
            Coverage,
            DepthGreaterEqual,
            DepthLessEqual,
            StencilRef,
            InnerCoverage,
        }

        enum DxbcComponentType
        {
            Unknown,
            Uint32,
            Int32,
            Float,

            Count
        }

        #endregion

        struct DxbcContext
        {
            internal const uint k_HeaderSize = 32;
            internal struct Header
            {
                internal uint magic;
                internal byte[] hash;
                internal uint version;
                internal uint size;
                internal uint numChunks;
            }

            internal Header header;
            internal DxbcSignature inputSignature;
            internal DxbcSignature outputSignature;
            internal DxbcShader shader;
        }

        struct DxbcSignature
        {
            internal struct Element
            {
                internal string name;
                internal uint semanticIndex;
                internal DxbcBuiltin valueType;
                internal DxbcComponentType componentType;
                internal uint registerIndex;
                internal byte mask;
                internal byte readWriteMask;
                internal byte stream;
            };

            internal uint key;
            internal List<Element> elements;
        }

        struct DxbcShader
        {
            internal uint version;
            internal byte[] byteCode;
            internal bool shex;
        }

        struct DxbcInstruction
        {
            internal enum ExtendedType
            {
                Empty,
                SampleControls,
                ResourceDim,
                ResourceReturnType,

                Count
            }

            internal DxbcOpcode opcode;
            internal uint[] value;
            internal uint length;
            internal int numOperands;
            internal ExtendedType[] extended;
            internal DxbcResourceDim srv;
            internal byte samples;
            internal DxbcInterpolation interpolation;
            internal bool shadow;
            internal bool mono;
            internal bool allowRefactoring;
            internal bool fp64;
            internal bool earlyDepth;
            internal bool enableBuffers;
            internal bool skipOptimization;
            internal bool enableMinPrecision;
            internal bool enableDoubleExtensions;
            internal bool enableShaderExtensions;
            internal bool threadsInGroup;
            internal bool sharedMemory;
            internal bool uavGroup;
            internal bool uavGlobal;
            internal DxbcResourceReturnType retType;
            internal bool saturate;
            internal byte testNZ;
            internal byte[] sampleOffsets;
            internal byte resourceTarget;
            internal byte resourceStride;
            internal DxbcResourceReturnType[] resourceReturnTypes;
            internal DxbcOperand[] operand;
            internal DxbcCustomDataClass customDataClass;
            internal List<uint> customData;

            internal void Init()
            {
                value = new uint[3];
                extended = new ExtendedType[3];
                sampleOffsets = new byte[3];
                resourceReturnTypes = new DxbcResourceReturnType[4];
                operand = new DxbcOperand[6];
                customData = new List<uint>();
            }
        }

        struct DxbcOperand
        {
            internal DxbcOperandType type;
            internal DxbcOperandMode mode;
            internal byte modeBits;
            internal byte num;
            internal DxbcOperandModifier modifier;
            internal byte numAddrModes;
            internal DxbcOperandAddrMode[] addrMode;
            internal uint[] regIndex;
            internal DxbcSubOperand[] subOperand;
            internal uint[] imm32;
            internal ulong[] imm64;

            internal void Init()
            {
                addrMode = new DxbcOperandAddrMode[3];
                regIndex = new uint[3];
                subOperand = new DxbcSubOperand[3];
                imm32 = new uint[4];
                imm64 = new ulong[4];
            }
        }

        struct DxbcSubOperand
        {
            internal DxbcOperandType type;
            internal byte mode;
            internal byte modeBits;
            internal byte num;
            internal byte numAddrModes;
            internal byte addrMode;
            internal uint regIndex;
        }

        struct CbDecl
        {
            //internal uint register;
            internal uint numComponents;
        }

        internal static byte[] PatchConstantBuffersAndTextures(byte[] bytecode, Dictionary<uint, uint> texToSampler)
        {
            var context = ReadChunks(bytecode);

#if DEBUG
            var written = WriteChunks(context);
            if (!StructuralComparisons.StructuralEqualityComparer.Equals(bytecode, written))
            {
                throw new InvalidDataException("Mismatch between DXBC read and write.");
            }
#endif

            var instructions = ParseInstructions(context.shader.byteCode, out List<CbDecl> cbDecls);

            using (BinaryWriter bw = new BinaryWriter(new MemoryStream()))
            {
                foreach (var instruction in instructions)
                {
                    bool write = true;
                    for (int iop = 0; iop < instruction.numOperands; iop++)
                    {
                        if (instruction.operand[iop].type == DxbcOperandType.Resource)
                        {
                            if (DxbcOperandAddrMode.Imm32 == instruction.operand[iop].addrMode[0]
                                || DxbcOperandAddrMode.RegImm32 == instruction.operand[iop].addrMode[0])
                            {
                                // Patch texture register to match sampler register. This includes declaration and usages
                                // This is needed because bgfx expects them to be the same and the Unity shader compiler uses automatic assignment, which for samplers is based on order of declaration and for textures is based on order of usage
                                var reg = instruction.operand[iop].regIndex[0];
                                if (texToSampler.ContainsKey(reg))
                                {
                                    instruction.operand[iop].regIndex[0] = texToSampler[reg];
                                }
                            }
                            else
                            {
                                // TODO
                                throw new NotSupportedException("Unsupported operand type in DXBC PatchConstantBuffersAndTextures.");
                            }
                        }
                        else if (instruction.operand[iop].type == DxbcOperandType.ConstantBuffer)
                        {
                            if (DxbcOperandAddrMode.Imm32 == instruction.operand[iop].addrMode[0])
                            {
                                for (var iaddr = 1; iaddr < instruction.operand[iop].numAddrModes; ++iaddr)
                                {
                                    if (instruction.opcode == DxbcOpcode.DCL_CONSTANT_BUFFER)
                                    {
                                        Assert.IsTrue(DxbcOperandAddrMode.Imm32 == instruction.operand[iop].addrMode[iaddr]
                                            || DxbcOperandAddrMode.RegImm32 == instruction.operand[iop].addrMode[iaddr]);

                                        // Patch cb0 declaration
                                        if (instruction.operand[iop].regIndex[0] == 0)
                                        {
                                            instruction.operand[iop].regIndex[iaddr] = 0;
                                            foreach (var cb in cbDecls)
                                                instruction.operand[iop].regIndex[iaddr] += cb.numComponents;
                                        }
                                        else
                                        {
                                            // Skip declarations for other cb's since their components are now part of cb0
                                            write = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        // Usage of constant. Modify component offset if not cb0
                                        // instruction.operand[i].regIndex[0] is the register
                                        // instruction.operand[i].regIndex[iaddr] is the component offset
                                        if (instruction.operand[iop].regIndex[0] != 0)
                                        {
                                            Assert.IsTrue(instruction.operand[iop].regIndex[0] < cbDecls.Count);

                                            if (DxbcOperandAddrMode.Imm32 == instruction.operand[iop].addrMode[iaddr]
                                                || DxbcOperandAddrMode.RegImm32 == instruction.operand[iop].addrMode[iaddr])
                                            {
                                                for (int icb = 0; icb < instruction.operand[iop].regIndex[0]; icb++)
                                                {
                                                    instruction.operand[iop].regIndex[iaddr] += cbDecls[icb].numComponents;
                                                }
                                            }
                                            else
                                            {
                                                // TODO
                                                throw new NotSupportedException("Unsupported operand type in DXBC PatchConstantBuffersAndTextures.");
//                                                operand.subOperand[jj].regIndex = operand.regIndex[jj];
//                                                operand.addrMode[jj] = DxbcOperandAddrMode::RegImm32;
//                                                operand.regIndex[jj] = cast.offset;
                                            }

                                            instruction.operand[iop].regIndex[0] = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (write)
                        WriteInstruction(bw, instruction);
                }

                context.shader.byteCode = (bw.BaseStream as MemoryStream)?.ToArray();

                // Make semantics match bgfx
                for (int i = 0; i < context.inputSignature.elements.Count; ++i)
                {
                    if (context.inputSignature.elements[i].name == "BLENDWEIGHTS")
                    {
                        var element = context.inputSignature.elements[i];
                        element.name = "BLENDWEIGHT";
                        context.inputSignature.elements[i] = element;
                        break;
                    }
                }

                return WriteChunks(context);
            }
        }

        static List<DxbcInstruction> ParseInstructions(byte[] shader, out List<CbDecl> cbDecls)
        {
            cbDecls = new List<CbDecl>();
            var instructions = new List<DxbcInstruction>();
            using (BinaryReader br = new BinaryReader(new MemoryStream(shader)))
            {
                var numTokens = shader.Length / sizeof(uint);
                uint token = 0;
                while (token < numTokens)
                {
                    var instruction = ReadInstruction(br);
                    for (int i = 0; i < instruction.numOperands; i++)
                    {
                        if (instruction.opcode == DxbcOpcode.DCL_CONSTANT_BUFFER && instruction.operand[i].type == DxbcOperandType.ConstantBuffer)
                        {
                            cbDecls.Add(new CbDecl { /*register = instruction.operand[i].regIndex[0],*/ numComponents = instruction.operand[i].regIndex[1] });
                            Assert.IsTrue(instruction.operand[i].regIndex[0] == cbDecls.Count - 1);
                        }
                    }

                    instructions.Add(instruction);
                    token += instruction.length;
                }
            }

            return instructions;
        }

        static DxbcContext ReadChunks(byte[] bytecode)
        {
            DxbcContext context = new DxbcContext();
            using (BinaryReader br = new BinaryReader(new MemoryStream(bytecode)))
            {
                br.BaseStream.Position = 0;
                context.header = ReadHeader(br);
                for (int i = 0; i < context.header.numChunks; i++)
                {
                    br.BaseStream.Position = DxbcContext.k_HeaderSize + i * sizeof(uint);
                    var chunkOffset = br.ReadUInt32();
                    br.BaseStream.Position = chunkOffset;

                    var fourcc = br.ReadUInt32();
                    var chunkSize = br.ReadUInt32();

                    if (fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'H', 'D', 'R'))
                    {
                        context.shader = ReadShader(br);

                    }
                    else if (fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'H', 'E', 'X'))
                    {
                        context.shader = ReadShader(br);
                        context.shader.shex = true;
                    }
                    else if (fourcc == BgfxHelper.BX_MAKEFOURCC('I', 'S', 'G', 'N'))
                    {
                        context.inputSignature = ReadSignature(br);
                    }
                    else if (fourcc == BgfxHelper.BX_MAKEFOURCC('O', 'S', 'G', '1')
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('O', 'S', 'G', '5')
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('O', 'S', 'G', 'N'))
                    {
                        context.outputSignature = ReadSignature(br);
                    }
                    else if (fourcc == BgfxHelper.BX_MAKEFOURCC('A', 'o', 'n', '9')) // Contains DX9BC for feature level 9.x (*s_4_0_level_9_*) shaders.
                    {
                        throw new NotSupportedException("DirectX Feature Level 9 is not supported.");
                    }
                    else if (fourcc == BgfxHelper.BX_MAKEFOURCC('I', 'F', 'C', 'E') // Interface.
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('R', 'D', 'E', 'F') // Resource definition.
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'D', 'G', 'B') // Shader debugging info (old).
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'P', 'D', 'B') // Shader debugging info (new).
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'F', 'I', '0') // ?
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('S', 'T', 'A', 'T') // Statistics.
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('P', 'C', 'S', 'G') // Patch constant signature.
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('P', 'S', 'O', '1') // Pipeline State Object 1
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('P', 'S', 'O', '2') // Pipeline State Object 2
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('X', 'N', 'A', 'P') // ?
                        || fourcc == BgfxHelper.BX_MAKEFOURCC('X', 'N', 'A', 'S')) // ?
                    {
                    }
                    else
                    {
                        throw new InvalidDataException($"Unrecognized FOURCC {(char)(fourcc >> 24 & 0xFF)}{(char)(fourcc >> 16 & 0xFF)}{(char)(fourcc >> 8 & 0xFF)}{(char)(fourcc & 0xFF)} when parsing DXBC.");
                    }
                }
            }

            return context;
        }

        static byte[] WriteChunks(DxbcContext context)
        {
            using (BinaryWriter bw = new BinaryWriter(new MemoryStream()))
            {
                // magic
                var dxbcOffset = bw.BaseStream.Position;
                bw.Write(BgfxHelper.BX_MAKEFOURCC('D', 'X', 'B', 'C'));

                // hash is calculated below
                bw.Write(new byte[16]);

                // version
                bw.Write(1u);

                // size
                var sizeOffset = bw.BaseStream.Position;
                bw.Write(0);

                var numChunks = 3u;
                bw.Write(numChunks);
                var chunksOffset = bw.BaseStream.Position;
                bw.Write(new byte[numChunks * sizeof(uint)]);

                uint[] chunkOffsets = new uint[3];
                uint[] chunkSize = new uint[3];

                chunkOffsets[0] = (uint)(bw.BaseStream.Position - dxbcOffset);
                bw.Write(BgfxHelper.BX_MAKEFOURCC('I', 'S', 'G', 'N'));
                bw.Write(0u);
                chunkSize[0] = WriteSignature(bw, context.inputSignature);

                chunkOffsets[1] = (uint)(bw.BaseStream.Position - dxbcOffset);
                bw.Write(BgfxHelper.BX_MAKEFOURCC('O', 'S', 'G', 'N'));
                bw.Write(0u);
                chunkSize[1] = WriteSignature(bw, context.outputSignature);

                chunkOffsets[2] = (uint)(bw.BaseStream.Position - dxbcOffset);
                bw.Write(context.shader.shex ? BgfxHelper.BX_MAKEFOURCC('S', 'H', 'E', 'X') : BgfxHelper.BX_MAKEFOURCC('S', 'H', 'D', 'R'));
                bw.Write(0u);
                chunkSize[2] = WriteShader(bw, context.shader);

                var eof = bw.BaseStream.Position;

                bw.BaseStream.Position = sizeOffset;
                bw.Write((uint)bw.BaseStream.Length);

                bw.BaseStream.Position = chunksOffset;
                foreach (var offset in chunkOffsets)
                {
                    bw.Write(offset);
                }

                for (int i = 0; i < chunkOffsets.Length; i++)
                {
                    bw.BaseStream.Position = chunkOffsets[i] + 4;
                    bw.Write(chunkSize[i]);
                }

                bw.BaseStream.Position = eof;

                var bytecode = (bw.BaseStream as MemoryStream)?.ToArray();
                DxbcHash(bytecode);
                return bytecode;
            }
        }

        static void DxbcHash(byte[] bytecode)
        {
            uint[] hash =
            {
                0x67452301,
                0xefcdab89,
                0x98badcfe,
                0x10325476,
            };

            var data = new uint[bytecode.Length / sizeof(uint)];
            Buffer.BlockCopy(bytecode, 0, data, 0, bytecode.Length);
            uint size = (uint)(bytecode.Length - 20);
            uint dataIndex = 20 / sizeof(uint);
            var num = size / 64;
            for (uint i = 0; i < num; ++i)
            {
                DxbcHashBlock(data, dataIndex, hash);
                dataIndex += 16;
            }

            var last = new uint[16];

            uint remaining = size & 0x3f;

            if (remaining >= 56)
            {
                Array.Copy(data, dataIndex, last, 0, remaining / sizeof(uint));
                last[remaining / 4] = 0x80;
                DxbcHashBlock(last, 0, hash);

                var zeros = new uint[56 / sizeof(uint)];
                Array.Copy(zeros, 0, last, 1, zeros.Length);
            }
            else
            {
                Array.Copy(data, dataIndex, last, 1, remaining / sizeof(uint));
                last[1 + remaining / 4] = 0x80;
            }

            last[0] = size * 8;
            last[15] = size * 2 + 1;
            DxbcHashBlock(last, 0, hash);

            Buffer.BlockCopy(hash, 0, bytecode, 4, hash.Length * sizeof(uint));
        }

        static void DxbcHashBlock(uint[] data, uint dataIndex, uint[] hash)
        {
            uint d0 = data[dataIndex + 0];
            uint d1 = data[dataIndex + 1];
            uint d2 = data[dataIndex + 2];
            uint d3 = data[dataIndex + 3];
            uint d4 = data[dataIndex + 4];
            uint d5 = data[dataIndex + 5];
            uint d6 = data[dataIndex + 6];
            uint d7 = data[dataIndex + 7];
            uint d8 = data[dataIndex + 8];
            uint d9 = data[dataIndex + 9];
            uint d10 = data[dataIndex + 10];
            uint d11 = data[dataIndex + 11];
            uint d12 = data[dataIndex + 12];
            uint d13 = data[dataIndex + 13];
            uint d14 = data[dataIndex + 14];
            uint d15 = data[dataIndex + 15];

            uint aa = hash[0];
            uint bb = hash[1];
            uint cc = hash[2];
            uint dd = hash[3];

            aa = bb + uint32_rol(aa + DxbcMixF(bb, cc, dd) + d0 + 0xd76aa478, 7);
            dd = aa + uint32_rol(dd + DxbcMixF(aa, bb, cc) + d1 + 0xe8c7b756, 12);
            cc = dd + uint32_ror(cc + DxbcMixF(dd, aa, bb) + d2 + 0x242070db, 15);
            bb = cc + uint32_ror(bb + DxbcMixF(cc, dd, aa) + d3 + 0xc1bdceee, 10);
            aa = bb + uint32_rol(aa + DxbcMixF(bb, cc, dd) + d4 + 0xf57c0faf, 7);
            dd = aa + uint32_rol(dd + DxbcMixF(aa, bb, cc) + d5 + 0x4787c62a, 12);
            cc = dd + uint32_ror(cc + DxbcMixF(dd, aa, bb) + d6 + 0xa8304613, 15);
            bb = cc + uint32_ror(bb + DxbcMixF(cc, dd, aa) + d7 + 0xfd469501, 10);
            aa = bb + uint32_rol(aa + DxbcMixF(bb, cc, dd) + d8 + 0x698098d8, 7);
            dd = aa + uint32_rol(dd + DxbcMixF(aa, bb, cc) + d9 + 0x8b44f7af, 12);
            cc = dd + uint32_ror(cc + DxbcMixF(dd, aa, bb) + d10 + 0xffff5bb1, 15);
            bb = cc + uint32_ror(bb + DxbcMixF(cc, dd, aa) + d11 + 0x895cd7be, 10);
            aa = bb + uint32_rol(aa + DxbcMixF(bb, cc, dd) + d12 + 0x6b901122, 7);
            dd = aa + uint32_rol(dd + DxbcMixF(aa, bb, cc) + d13 + 0xfd987193, 12);
            cc = dd + uint32_ror(cc + DxbcMixF(dd, aa, bb) + d14 + 0xa679438e, 15);
            bb = cc + uint32_ror(bb + DxbcMixF(cc, dd, aa) + d15 + 0x49b40821, 10);

            aa = bb + uint32_rol(aa + DxbcMixG(bb, cc, dd) + d1 + 0xf61e2562, 5);
            dd = aa + uint32_rol(dd + DxbcMixG(aa, bb, cc) + d6 + 0xc040b340, 9);
            cc = dd + uint32_rol(cc + DxbcMixG(dd, aa, bb) + d11 + 0x265e5a51, 14);
            bb = cc + uint32_ror(bb + DxbcMixG(cc, dd, aa) + d0 + 0xe9b6c7aa, 12);
            aa = bb + uint32_rol(aa + DxbcMixG(bb, cc, dd) + d5 + 0xd62f105d, 5);
            dd = aa + uint32_rol(dd + DxbcMixG(aa, bb, cc) + d10 + 0x02441453, 9);
            cc = dd + uint32_rol(cc + DxbcMixG(dd, aa, bb) + d15 + 0xd8a1e681, 14);
            bb = cc + uint32_ror(bb + DxbcMixG(cc, dd, aa) + d4 + 0xe7d3fbc8, 12);
            aa = bb + uint32_rol(aa + DxbcMixG(bb, cc, dd) + d9 + 0x21e1cde6, 5);
            dd = aa + uint32_rol(dd + DxbcMixG(aa, bb, cc) + d14 + 0xc33707d6, 9);
            cc = dd + uint32_rol(cc + DxbcMixG(dd, aa, bb) + d3 + 0xf4d50d87, 14);
            bb = cc + uint32_ror(bb + DxbcMixG(cc, dd, aa) + d8 + 0x455a14ed, 12);
            aa = bb + uint32_rol(aa + DxbcMixG(bb, cc, dd) + d13 + 0xa9e3e905, 5);
            dd = aa + uint32_rol(dd + DxbcMixG(aa, bb, cc) + d2 + 0xfcefa3f8, 9);
            cc = dd + uint32_rol(cc + DxbcMixG(dd, aa, bb) + d7 + 0x676f02d9, 14);
            bb = cc + uint32_ror(bb + DxbcMixG(cc, dd, aa) + d12 + 0x8d2a4c8a, 12);

            aa = bb + uint32_rol(aa + DxbcMixH(bb, cc, dd) + d5 + 0xfffa3942, 4);
            dd = aa + uint32_rol(dd + DxbcMixH(aa, bb, cc) + d8 + 0x8771f681, 11);
            cc = dd + uint32_rol(cc + DxbcMixH(dd, aa, bb) + d11 + 0x6d9d6122, 16);
            bb = cc + uint32_ror(bb + DxbcMixH(cc, dd, aa) + d14 + 0xfde5380c, 9);
            aa = bb + uint32_rol(aa + DxbcMixH(bb, cc, dd) + d1 + 0xa4beea44, 4);
            dd = aa + uint32_rol(dd + DxbcMixH(aa, bb, cc) + d4 + 0x4bdecfa9, 11);
            cc = dd + uint32_rol(cc + DxbcMixH(dd, aa, bb) + d7 + 0xf6bb4b60, 16);
            bb = cc + uint32_ror(bb + DxbcMixH(cc, dd, aa) + d10 + 0xbebfbc70, 9);
            aa = bb + uint32_rol(aa + DxbcMixH(bb, cc, dd) + d13 + 0x289b7ec6, 4);
            dd = aa + uint32_rol(dd + DxbcMixH(aa, bb, cc) + d0 + 0xeaa127fa, 11);
            cc = dd + uint32_rol(cc + DxbcMixH(dd, aa, bb) + d3 + 0xd4ef3085, 16);
            bb = cc + uint32_ror(bb + DxbcMixH(cc, dd, aa) + d6 + 0x04881d05, 9);
            aa = bb + uint32_rol(aa + DxbcMixH(bb, cc, dd) + d9 + 0xd9d4d039, 4);
            dd = aa + uint32_rol(dd + DxbcMixH(aa, bb, cc) + d12 + 0xe6db99e5, 11);
            cc = dd + uint32_rol(cc + DxbcMixH(dd, aa, bb) + d15 + 0x1fa27cf8, 16);
            bb = cc + uint32_ror(bb + DxbcMixH(cc, dd, aa) + d2 + 0xc4ac5665, 9);

            aa = bb + uint32_rol(aa + DxbcMixI(bb, cc, dd) + d0 + 0xf4292244, 6);
            dd = aa + uint32_rol(dd + DxbcMixI(aa, bb, cc) + d7 + 0x432aff97, 10);
            cc = dd + uint32_rol(cc + DxbcMixI(dd, aa, bb) + d14 + 0xab9423a7, 15);
            bb = cc + uint32_ror(bb + DxbcMixI(cc, dd, aa) + d5 + 0xfc93a039, 11);
            aa = bb + uint32_rol(aa + DxbcMixI(bb, cc, dd) + d12 + 0x655b59c3, 6);
            dd = aa + uint32_rol(dd + DxbcMixI(aa, bb, cc) + d3 + 0x8f0ccc92, 10);
            cc = dd + uint32_rol(cc + DxbcMixI(dd, aa, bb) + d10 + 0xffeff47d, 15);
            bb = cc + uint32_ror(bb + DxbcMixI(cc, dd, aa) + d1 + 0x85845dd1, 11);
            aa = bb + uint32_rol(aa + DxbcMixI(bb, cc, dd) + d8 + 0x6fa87e4f, 6);
            dd = aa + uint32_rol(dd + DxbcMixI(aa, bb, cc) + d15 + 0xfe2ce6e0, 10);
            cc = dd + uint32_rol(cc + DxbcMixI(dd, aa, bb) + d6 + 0xa3014314, 15);
            bb = cc + uint32_ror(bb + DxbcMixI(cc, dd, aa) + d13 + 0x4e0811a1, 11);
            aa = bb + uint32_rol(aa + DxbcMixI(bb, cc, dd) + d4 + 0xf7537e82, 6);
            dd = aa + uint32_rol(dd + DxbcMixI(aa, bb, cc) + d11 + 0xbd3af235, 10);
            cc = dd + uint32_rol(cc + DxbcMixI(dd, aa, bb) + d2 + 0x2ad7d2bb, 15);
            bb = cc + uint32_ror(bb + DxbcMixI(cc, dd, aa) + d9 + 0xeb86d391, 11);

            hash[0] += aa;
            hash[1] += bb;
            hash[2] += cc;
            hash[3] += dd;
        }

        static uint uint32_rol(uint _a, int _sa)
        {
            return (_a << _sa) | (_a >> (32 - _sa));
        }

        static uint uint32_ror(uint _a, int _sa)
        {
            return (_a >> _sa) | (_a << (32 - _sa));
        }

        static uint DxbcMixF(uint _b, uint _c, uint _d)
        {
            uint tmp0 = _c ^ _d;
            uint tmp1 = _b & tmp0;
            uint result = _d ^ tmp1;

            return result;
        }

        static uint DxbcMixG(uint _b, uint _c, uint _d)
        {
            return DxbcMixF(_d, _b, _c);
        }

        static uint DxbcMixH(uint _b, uint _c, uint _d)
        {
            uint tmp0 = _b ^ _c;
            uint result = _d ^ tmp0;

            return result;
        }

        static uint DxbcMixI(uint _b, uint _c, uint _d)
        {
            uint tmp0 = _b | ~_d;
            uint result = _c ^ tmp0;

            return result;
        }

        static DxbcContext.Header ReadHeader(BinaryReader br)
        {
            DxbcContext.Header header = new DxbcContext.Header();
            header.magic = br.ReadUInt32();
            header.hash = br.ReadBytes(16);
            header.version = br.ReadUInt32();
            header.size = br.ReadUInt32();
            header.numChunks = br.ReadUInt32();
            return header;
        }

        static DxbcSignature ReadSignature(BinaryReader br)
        {
            DxbcSignature signature = new DxbcSignature();
            signature.elements = new List<DxbcSignature.Element>();
            var offset = br.BaseStream.Position;
            var num = br.ReadUInt32();
            signature.key = br.ReadUInt32();

            for (var i = 0; i < num; ++i)
            {
                var element = new DxbcSignature.Element();
                uint nameOffset = br.ReadUInt32();

                var oldOffset = br.BaseStream.Position;
                br.BaseStream.Position = offset + nameOffset;
                StringBuilder sb = new StringBuilder();
                var c = br.ReadChar();
                while (c != '\0')
                {
                    sb.Append(c);
                    c = br.ReadChar();
                }
                element.name = sb.ToString();
                br.BaseStream.Position = oldOffset;

                element.semanticIndex = br.ReadUInt32();
                element.valueType = (DxbcBuiltin)br.ReadUInt32();
                element.componentType = (DxbcComponentType)br.ReadUInt32();
                element.registerIndex = br.ReadUInt32();
                element.mask = br.ReadByte();
                element.readWriteMask = br.ReadByte();
                element.stream = br.ReadByte();

                // padding
                br.ReadByte();

                signature.elements.Add(element);
            }

            return signature;
        }

        static uint WriteSignature(BinaryWriter bw, DxbcSignature signature)
        {
            var startSize = bw.BaseStream.Length;
            bw.Write((uint)signature.elements.Count);
            bw.Write(signature.key);

            Dictionary<string, uint> nameOffsetMap = new Dictionary<string, uint>();
            uint nameOffset = (uint)signature.elements.Count * 24 + 8;
            foreach (var element in signature.elements)
            {
                if (!nameOffsetMap.ContainsKey(element.name))
                {
                    nameOffsetMap[element.name] = nameOffset;
                    bw.Write(nameOffset);
                    nameOffset += (uint)(element.name.Length + 1);
                }
                else
                {
                    bw.Write(nameOffsetMap[element.name]);
                }

                bw.Write(element.semanticIndex);
                bw.Write((uint)element.valueType);
                bw.Write((uint)element.componentType);
                bw.Write(element.registerIndex);
                bw.Write(element.mask);
                bw.Write(element.readWriteMask);
                bw.Write(element.stream);
                byte pad = 0;
                bw.Write(pad);
            }

            int len = 0;
            foreach (var element in signature.elements)
            {
                if (nameOffsetMap.Remove(element.name))
                {
                    bw.Write((element.name + "\0").ToCharArray());
                    len += element.name.Length + 1;
                }
            }

            // align 4 bytes
            var padding = Enumerable.Repeat((byte)0xab, (len + 3) / 4 * 4 - len).ToArray();
            bw.Write(padding);

            return (uint)(bw.BaseStream.Length - startSize);
        }

        static DxbcShader ReadShader(BinaryReader br)
        {
            DxbcShader shader = new DxbcShader();
            shader.version = br.ReadUInt32();
            var bcLength = br.ReadUInt32();
            var len = (bcLength - 2) * sizeof(uint);
            shader.byteCode = br.ReadBytes((int)len);
            return shader;
        }

        static uint WriteShader(BinaryWriter bw, DxbcShader shader)
        {
            var startSize = bw.BaseStream.Length;

            var len = (uint)(shader.byteCode.Length);
            var bcLength = len / sizeof(uint) + 2;

            bw.Write(shader.version);
            bw.Write(bcLength);
            bw.Write(shader.byteCode);

            return (uint)(bw.BaseStream.Length - startSize);
        }

        static DxbcInstruction ReadInstruction(BinaryReader br)
        {
            DxbcInstruction instruction = new DxbcInstruction();
            instruction.Init();

            var token = br.ReadUInt32();

            // 0       1       2       3
            // 76543210765432107654321076543210
            // elllllll.............ooooooooooo
            // ^^                   ^----------- opcode
            // |+------------------------------- length
            // +-------------------------------- extended

            instruction.opcode = (DxbcOpcode)(token & 0x000007ff);
            if (instruction.opcode >= DxbcOpcode.Count)
                throw new InvalidDataException($"Unrecognized DXBC opcode {instruction.opcode}.");

            instruction.length = (byte)((token & 0x7f000000) >> 24);
            bool extended = 0 != (token & 0x80000000);

            instruction.srv = DxbcResourceDim.Unknown;
            instruction.samples = 0;

            instruction.shadow = false;
            instruction.mono = false;

            instruction.allowRefactoring = false;
            instruction.fp64 = false;
            instruction.earlyDepth = false;
            instruction.enableBuffers = false;
            instruction.skipOptimization = false;
            instruction.enableMinPrecision = false;
            instruction.enableDoubleExtensions = false;
            instruction.enableShaderExtensions = false;

            instruction.threadsInGroup = false;
            instruction.sharedMemory = false;
            instruction.uavGroup = false;
            instruction.uavGlobal = false;

            instruction.saturate = false;
            instruction.testNZ = 0;
            instruction.retType = DxbcResourceReturnType.Unused;

            instruction.customDataClass = DxbcCustomDataClass.Comment;

            switch (instruction.opcode)
            {
                case DxbcOpcode.CUSTOMDATA:
                    {
                        instruction.customDataClass = (DxbcCustomDataClass)((token & 0xfffff800) >> 11);

                        instruction.numOperands = 0;
                        instruction.length = br.ReadUInt32();
                        var num = (instruction.length - 2);
                        for (var i = 0; i < num; ++i)
                        {
                            uint temp = br.ReadUInt32();
                            instruction.customData.Add(temp);
                        }
                    }
                    return instruction;

                case DxbcOpcode.DCL_CONSTANT_BUFFER:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........            a...........
                    //                     ^------------ Allow refactoring

                    instruction.allowRefactoring = 0 != (token & 0x00000800);
                    break;

                case DxbcOpcode.DCL_GLOBAL_FLAGS:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........     sxmoudfa...........
                    //              ^^^^^^^^------------ Allow refactoring
                    //              ||||||+------------- FP64
                    //              |||||+-------------- Force early depth/stencil
                    //              ||||+--------------- Enable raw and structured buffers
                    //              |||+---------------- Skip optimizations
                    //              ||+----------------- Enable minimum precision
                    //              |+------------------ Enable double extension
                    //              +------------------- Enable shader extension

                    instruction.allowRefactoring = 0 != (token & 0x00000800);
                    instruction.fp64 = 0 != (token & 0x00001000);
                    instruction.earlyDepth = 0 != (token & 0x00002000);
                    instruction.enableBuffers = 0 != (token & 0x00004000);
                    instruction.skipOptimization = 0 != (token & 0x00008000);
                    instruction.enableMinPrecision = 0 != (token & 0x00010000);
                    instruction.enableDoubleExtensions = 0 != (token & 0x00020000);
                    instruction.enableShaderExtensions = 0 != (token & 0x00040000);
                    break;

                case DxbcOpcode.DCL_INPUT_PS:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........        iiiii...........
                    //                 ^---------------- Interploation

                    instruction.interpolation = (DxbcInterpolation)((token & 0x0000f800) >> 11);
                    break;

                case DxbcOpcode.DCL_RESOURCE:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........ sssssssrrrrr...........
                    //          ^      ^---------------- SRV
                    //          +----------------------- MSAA samples

                    instruction.srv = (DxbcResourceDim)((token & 0x0000f800) >> 11);
                    instruction.samples = (byte)((token & 0x007f0000) >> 16);
                    break;

                case DxbcOpcode.DCL_SAMPLER:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........           ms...........
                    //                    ^^------------ Shadow sampler
                    //                    +------------- Mono

                    instruction.shadow = 0 != (token & 0x00000800);
                    instruction.mono = 0 != (token & 0x00001000);
                    break;

                case DxbcOpcode.SYNC:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........         gust...........
                    //                  ^^^^------------ Threads in group
                    //                  ||+------------- Shared memory
                    //                  |+-------------- UAV group
                    //                  +--------------- UAV global

                    instruction.threadsInGroup = 0 != (token & 0x00000800);
                    instruction.sharedMemory = 0 != (token & 0x00001000);
                    instruction.uavGroup = 0 != (token & 0x00002000);
                    instruction.uavGlobal = 0 != (token & 0x00004000);
                    break;

                default:
                    // 0       1       2       3
                    // 76543210765432107654321076543210
                    // ........ ppppn    stt...........
                    //          ^   ^    ^^------------- Resource info return type
                    //          |   |    +-------------- Saturate
                    //          |   +------------------- Test not zero
                    //          +----------------------- Precise mask

                    instruction.retType = (DxbcResourceReturnType)((token & 0x00001800) >> 11);
                    instruction.saturate = 0 != (token & 0x00002000);
                    instruction.testNZ = Convert.ToByte(0 != (token & 0x00040000));
                    break;
            }

            instruction.extended[0] = DxbcInstruction.ExtendedType.Count;
            uint iextended = 0;
            while (extended && iextended < instruction.extended.Length - 1)
            {
                // 0       1       2       3
                // 76543210765432107654321076543210
                // e..........................ttttt
                // ^                          ^
                // |                          +----- type
                // +-------------------------------- extended

                var extBits = br.ReadUInt32();
                extended = 0 != (extBits & 0x80000000);
                instruction.extended[iextended] = (DxbcInstruction.ExtendedType)(extBits & 0x0000001f);
                instruction.extended[iextended + 1] = DxbcInstruction.ExtendedType.Count;

                switch (instruction.extended[iextended])
                {
                    case DxbcInstruction.ExtendedType.SampleControls:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .          zzzzyyyyxxxx    .....
                        //            ^   ^   ^
                        //            |   |   +------------- x
                        //            |   +----------------- y
                        //            +--------------------- z

                        instruction.sampleOffsets[0] = (byte)((extBits & 0x00001e00) >> 9);
                        instruction.sampleOffsets[1] = (byte)((extBits & 0x0001e000) >> 13);
                        instruction.sampleOffsets[2] = (byte)((extBits & 0x001e0000) >> 17);
                        break;

                    case DxbcInstruction.ExtendedType.ResourceDim:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .                          .....
                        //

                        instruction.resourceTarget = (byte)((extBits & 0x000003e0) >> 6);
                        instruction.resourceStride = (byte)((extBits & 0x0000f800) >> 11);
                        break;

                    case DxbcInstruction.ExtendedType.ResourceReturnType:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .          3333222211110000.....
                        //            ^   ^   ^
                        //            |   |   +------------- x
                        //            |   +----------------- y
                        //            +--------------------- z

                        instruction.resourceReturnTypes[0] = (DxbcResourceReturnType)((extBits & 0x000001e0) >> 6);
                        instruction.resourceReturnTypes[1] = (DxbcResourceReturnType)((extBits & 0x00001e00) >> 9);
                        instruction.resourceReturnTypes[2] = (DxbcResourceReturnType)((extBits & 0x0001e000) >> 13);
                        instruction.resourceReturnTypes[3] = (DxbcResourceReturnType)((extBits & 0x001e0000) >> 17);
                        break;
                }

                iextended++;
            }

            switch (instruction.opcode)
            {
                case DxbcOpcode.DCL_FUNCTION_TABLE:
                {
                    var tableId = br.ReadUInt32();
                    var num = br.ReadUInt32();
                    for (int i = 0; i < num; i++)
                    {
                        var bodyId = br.ReadUInt32();
                    }
                    break;
                }

                case DxbcOpcode.DCL_INTERFACE:
                {
                    var interfaceId = br.ReadUInt32();
                    var num = br.ReadUInt32();
                    throw new NotImplementedException($"DXBC opcode {DxbcOpcode.DCL_INTERFACE} not supported");
                }
            }

            DxbcOpcodeInfo info = s_DxbcOpcodeInfo[(int)instruction.opcode];
            instruction.numOperands = info.numOperands;
            for (int i = 0; i < instruction.numOperands; i++)
            {
                instruction.operand[i] = ReadOperand(br);
            }

            for (int i = 0; i < info.numValues; i++)
            {
                instruction.value[i] = br.ReadUInt32();
            }

            return instruction;
        }

        static void WriteInstruction(BinaryWriter bw, DxbcInstruction instruction)
        {
            uint token = 0;
            token |= ((uint)instruction.opcode) & 0x000007ff;
            token |= (instruction.length << 24) & 0x7f000000;

            token |= DxbcInstruction.ExtendedType.Count != instruction.extended[0] ? 0x80000000  : 0;

            switch (instruction.opcode)
            {
                case DxbcOpcode.CUSTOMDATA:
                {
                    token &= 0x000007ff;
                    token |= (uint)(instruction.customDataClass) << 11;

                    bw.Write(token);

                    uint len = (uint)(instruction.customData.Count * sizeof(uint));
                    bw.Write(len / 4 + 2);
                    foreach (var customData in instruction.customData)
                    {
                        bw.Write(customData);
                    }

                    return;
                }
                case DxbcOpcode.DCL_CONSTANT_BUFFER:
                    token |= instruction.allowRefactoring ? 0x00000800 : 0u;
                    break;
                case DxbcOpcode.DCL_GLOBAL_FLAGS:
                    token |= instruction.allowRefactoring ? 0x00000800 : 0u;
                    token |= instruction.fp64 ? 0x00001000 : 0u;
                    token |= instruction.earlyDepth ? 0x00002000 : 0u;
                    token |= instruction.enableBuffers ? 0x00004000 : 0u;
                    token |= instruction.skipOptimization ? 0x00008000 : 0u;
                    token |= instruction.enableMinPrecision ? 0x00010000 : 0u;
                    token |= instruction.enableDoubleExtensions ? 0x00020000 : 0u;
                    token |= instruction.enableShaderExtensions ? 0x00040000 : 0u;
                    break;
                case DxbcOpcode.DCL_INPUT_PS:
                    token |= ((uint)(instruction.interpolation) << 11) & 0x0000f800;
                    break;
                case DxbcOpcode.DCL_RESOURCE:
                    token |= ((uint)(instruction.srv) << 11) & 0x0000f800;
                    token |= ((uint)(instruction.samples) << 16) & 0x007f0000;
                    break;
                case DxbcOpcode.DCL_SAMPLER:
                    token |= instruction.shadow ? 0x00000800 : 0u;
                    token |= instruction.mono ? 0x00001000 : 0u;
                    break;
                case DxbcOpcode.SYNC:
                    token |= instruction.threadsInGroup ? 0x00000800 : 0u;
                    token |= instruction.sharedMemory ? 0x00001000 : 0u;
                    token |= instruction.uavGroup ? 0x00002000 : 0u;
                    token |= instruction.uavGlobal ? 0x00004000 : 0u;
                    break;
                default:
                    token |= ((uint)(instruction.retType) << 11) & 0x00001800;
                    token |= instruction.saturate ? 0x00002000 : 0u;
                    token |= Convert.ToBoolean(instruction.testNZ) ? 0x00040000 : 0u;
                    break;
            }

            bw.Write(token);

            for (uint i = 0; instruction.extended[i] != DxbcInstruction.ExtendedType.Count; ++i)
            {
                // 0       1       2       3
                // 76543210765432107654321076543210
                // e..........................ttttt
                // ^                          ^
                // |                          +----- type
                // +-------------------------------- extended

                token = instruction.extended[i + 1] == DxbcInstruction.ExtendedType.Count
                    ? 0
                    : 0x80000000
                    ;
                token |= (byte)(instruction.extended[i]);

                switch (instruction.extended[i])
                {
                    case DxbcInstruction.ExtendedType.SampleControls:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .          zzzzyyyyxxxx    .....
                        //            ^   ^   ^
                        //            |   |   +------------- x
                        //            |   +----------------- y
                        //            +--------------------- z

                        token |= ((uint)(instruction.sampleOffsets[0]) << 9) & 0x00001e00;
                        token |= ((uint)(instruction.sampleOffsets[1]) << 13) & 0x0001e000;
                        token |= ((uint)(instruction.sampleOffsets[2]) << 17) & 0x001e0000;
                        break;

                    case DxbcInstruction.ExtendedType.ResourceDim:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .                          .....
                        //

                        token |= ((uint)(instruction.resourceTarget << 6) & 0x000003e0);
                        token |= ((uint)(instruction.resourceStride << 11) & 0x0000f800);
                        break;

                    case DxbcInstruction.ExtendedType.ResourceReturnType:
                        // 0       1       2       3
                        // 76543210765432107654321076543210
                        // .          3333222211110000.....
                        //            ^   ^   ^
                        //            |   |   +------------- x
                        //            |   +----------------- y
                        //            +--------------------- z

                        token |= ((uint)(instruction.resourceReturnTypes[0]) << 6) & 0x000001e0;
                        token |= ((uint)(instruction.resourceReturnTypes[1]) << 9) & 0x00001e00;
                        token |= ((uint)(instruction.resourceReturnTypes[2]) << 13) & 0x0001e000;
                        token |= ((uint)(instruction.resourceReturnTypes[3]) << 17) & 0x001e0000;
                        break;
                }

                bw.Write(token);
            }

            for (var i = 0; i < instruction.numOperands; ++i)
            {
                WriteOperand(bw, instruction.operand[i]);
            }

            DxbcOpcodeInfo info = s_DxbcOpcodeInfo[(int)instruction.opcode];
            for (int i = 0; i < info.numValues; i++)
            {
                bw.Write(instruction.value[i]);
            }
        }

        static DxbcOperand ReadOperand(BinaryReader br)
        {
            DxbcOperand operand = new DxbcOperand();
            operand.Init();

            var token = br.ReadUInt32();

            // 0       1       2       3
            // 76543210765432107654321076543210
            // e222111000nnttttttttssssssssmmoo
            // ^^  ^  ^  ^ ^       ^       ^ ^-- number of operands
            // ||  |  |  | |       |       +---- operand mode
            // ||  |  |  | |       +------------ operand mode bits
            // ||  |  |  | +-------------------- type
            // ||  |  |  +---------------------- number of addressing modes
            // ||  |  +------------------------- addressing mode 0
            // ||  +---------------------------- addressing mode 1
            // |+------------------------------- addressing mode 2
            // +-------------------------------- extended

            operand.numAddrModes = (byte)((token & 0x00300000) >> 20);
            operand.addrMode[0] = (DxbcOperandAddrMode)((token & 0x01c00000) >> 22);
            operand.addrMode[1] = (DxbcOperandAddrMode)((token & 0x0e000000) >> 25);
            operand.addrMode[2] = (DxbcOperandAddrMode)((token & 0x70000000) >> 28);
            operand.type = (DxbcOperandType)((token & 0x000ff000) >> 12);
            operand.mode = (DxbcOperandMode)((token & 0x0000000c) >> 2);
            operand.modeBits = (byte)(((token & 0x00000ff0) >> 4) & "\x0f\xff\x03\x00"[(int)operand.mode]);
            operand.num = (byte)((token & 0x00000003));

            bool extended = 0 != (token & 0x80000000);
            if (extended)
            {
                var extBits = br.ReadUInt32();
                operand.modifier = (DxbcOperandModifier)((extBits & 0x00003fc0) >> 6);
            }
            else
            {
                operand.modifier = DxbcOperandModifier.None;
            }

            switch (operand.type)
            {
                case DxbcOperandType.Imm32:
                {
                    operand.num = 2 == operand.num ? (byte)4 : operand.num;
                    for (var i = 0; i < operand.num; i++)
                    {
                        operand.imm32[i] = br.ReadUInt32();
                    }
                    break;
                }

                case DxbcOperandType.Imm64:
                {
                    operand.num = 2 == operand.num ? (byte)4 : operand.num;
                    for (var i = 0; i < operand.num; i++)
                    {
                        operand.imm64[i] = br.ReadUInt64();
                    }
                    break;
                }
            }

            for (var i = 0; i < operand.numAddrModes; i++)
            {
                switch (operand.addrMode[i])
                {
                    case DxbcOperandAddrMode.Imm32:
                        operand.regIndex[i] = br.ReadUInt32();
                        break;
                    case DxbcOperandAddrMode.Reg:
                        operand.subOperand[i] = ReadSubOperand(br);
                        break;
                    case DxbcOperandAddrMode.RegImm32:
                        operand.regIndex[i] = br.ReadUInt32();
                        operand.subOperand[i] = ReadSubOperand(br);
                        break;
                }
            }

            return operand;
        }

        static void WriteOperand(BinaryWriter bw, DxbcOperand operand)
        {
            bool extended = operand.modifier != DxbcOperandModifier.None;

            uint token = 0;
            token |= extended ? 0x80000000 : 0;
            token |= (uint)(operand.numAddrModes << 20) & 0x00300000;
            token |= ((uint)operand.addrMode[0] << 22) & 0x01c00000;
            token |= ((uint)operand.addrMode[1] << 25) & 0x0e000000;
            token |= ((uint)operand.addrMode[2] << 28) & 0x70000000;
            token |= ((uint)operand.type << 12) & 0x000ff000;
            token |= ((uint)operand.mode << 2) & 0x0000000c;

            token |= (4 == operand.num ? 2u : operand.num) & 0x00000003;
            token |= (((uint)operand.modeBits & "\x0f\xff\x03\x00"[(int)operand.mode]) << 4) & 0x00000ff0;

            bw.Write(token);

            if (extended)
            {
                uint extBits = 0
                    | (((uint)operand.modifier << 6) & 0x00003fc0)
                    | 1 /* 1 == has extended operand modifier */
                    ;
                bw.Write(extBits);
            }

            switch (operand.type)
            {
                case DxbcOperandType.Imm32:
                    for (uint i = 0; i < operand.num; ++i)
                    {
                        bw.Write(operand.imm32[i]);
                    }
                    break;

                case DxbcOperandType.Imm64:
                    for (uint i = 0; i < operand.num; ++i)
                    {
                        bw.Write(operand.imm64[i]);
                    }
                    break;
            }

            var num = Math.Min(Convert.ToInt32(operand.numAddrModes), operand.addrMode.Length);
            for (var i = 0; i < num; ++i)
            {
                switch (operand.addrMode[i])
                {
                    case DxbcOperandAddrMode.Imm32:
                        bw.Write(operand.regIndex[i]);
                        break;

                    case DxbcOperandAddrMode.Reg:
                        WriteSubOperand(bw, operand.subOperand[i]);
                        break;

                    case DxbcOperandAddrMode.RegImm32:
                        bw.Write(operand.regIndex[i]);
                        WriteSubOperand(bw, operand.subOperand[i]);
                        break;
                }
            }
        }

        static DxbcSubOperand ReadSubOperand(BinaryReader br)
        {
            DxbcSubOperand subOperand = new DxbcSubOperand();

            var token = br.ReadUInt32();

            // 0       1       2       3
            // 76543210765432107654321076543210
            // e222111000nnttttttttssssssssmmoo
            // ^^  ^  ^  ^ ^       ^       ^ ^-- number of operands
            // ||  |  |  | |       |       +---- operand mode
            // ||  |  |  | |       +------------ operand mode bits
            // ||  |  |  | +-------------------- type
            // ||  |  |  +---------------------- number of addressing modes
            // ||  |  +------------------------- addressing mode 0
            // ||  +---------------------------- addressing mode 1
            // |+------------------------------- addressing mode 2
            // +-------------------------------- extended

            subOperand.type = (DxbcOperandType)((token & 0x000ff000) >> 12);
            subOperand.numAddrModes = (byte)((token & 0x00300000) >> 20);
            subOperand.addrMode = (byte)((token & 0x01c00000) >> 22);
            subOperand.mode = (byte)((token & 0x0000000c) >> 2);
            subOperand.modeBits = (byte)(((token & 0x00000ff0) >> 4) & "\x0f\xff\x03\x00"[subOperand.mode]);
            subOperand.num = (byte)((token & 0x00000003));

            switch ((DxbcOperandAddrMode)subOperand.addrMode)
            {
                case DxbcOperandAddrMode.Imm32:
                {
                    subOperand.regIndex = br.ReadUInt32();
                    break;
                }
                case DxbcOperandAddrMode.Reg:
                    ReadSubOperand(br);
                    break;
                case DxbcOperandAddrMode.RegImm32:
                {
                    subOperand.regIndex = br.ReadUInt32();
                    ReadSubOperand(br);
                    break;
                }
                case DxbcOperandAddrMode.RegImm64:
                {
                    subOperand.regIndex = br.ReadUInt32();
                    subOperand.regIndex = br.ReadUInt32();
                    ReadSubOperand(br);
                    break;
                }
            }

            return subOperand;
        }

        static void WriteSubOperand(BinaryWriter bw, DxbcSubOperand subOperand)
        {
            uint token = 0;
            token |= ((uint)subOperand.type << 12) & 0x000ff000;
            token |= ((uint)subOperand.numAddrModes << 20) & 0x00300000;
            token |= ((uint)subOperand.addrMode << 22) & 0x01c00000;
            token |= ((uint)subOperand.mode << 2) & 0x0000000c;
            token |= ((uint)subOperand.modeBits << 4) & 0x00000ff0;
            token |= (uint)subOperand.num & 0x00000003;
            bw.Write(token);

            switch ((DxbcOperandAddrMode)subOperand.addrMode)
            {
                case DxbcOperandAddrMode.Imm32:
                    bw.Write(subOperand.regIndex);
                    break;

                case DxbcOperandAddrMode.Reg:
                {
                    DxbcSubOperand temp = new DxbcSubOperand();
                    WriteSubOperand(bw, temp);
                    break;
                }
                case DxbcOperandAddrMode.RegImm32:
                {
                    bw.Write(subOperand.regIndex);
                    DxbcSubOperand temp = new DxbcSubOperand();
                    WriteSubOperand(bw, temp);
                    break;
                }
                case DxbcOperandAddrMode.RegImm64:
                {
                    bw.Write(subOperand.regIndex);
                    bw.Write(subOperand.regIndex);
                    DxbcSubOperand temp = new DxbcSubOperand();
                    WriteSubOperand(bw, temp);
                    break;
                }
            }
        }
    }
}
