%module RL
%{
#include <ecl/ecl.h>

int debug = 0;

int boot(void) {
    char *argv[] = {"RL"};
    return cl_boot(0, argv);
}

void set_debug(int level) {
    debug = level;
}

bool nil(cl_object o) {
    return Null(o);
}

cl_object num(int x) {
    return ecl_make_fixnum(x);
}

cl_object floatnum(float x) {
    return ecl_make_single_float(x);
}

cl_object str(const char *s) {
    return ecl_cstring_to_base_string_or_nil(s);
}

const char* cstr(const cl_object o) {
    return ecl_base_string_pointer_safe(si_coerce_to_base_string(o));
}

int cint(const cl_object o) {
    return ecl_to_int(o);
}

float cfloat(const cl_object o) {
    return ecl_to_float(o);
}

cl_object list() {
    return ECL_NIL;
}

cl_object list2(cl_object a, cl_object b) {
    return cl_list(2, a, b);
}

cl_object list3(cl_object a, cl_object b, cl_object c) {
    return cl_list(3, a, b, c);
}

cl_object call(const char *s) {
    return cl_list(1, ecl_read_from_cstring(s));
}

cl_object add(cl_object l, cl_object arg) {
    return cl_cons(arg, l);
}

cl_object readstr(const char *s) {
    return ecl_read_from_cstring(s);
}

cl_object eval(cl_object form) {
    cl_object ret = NULL;
    cl_env_ptr env = ecl_process_env();
    ECL_CATCH_ALL_BEGIN(env) {
        if (debug) {
            ret = si_safe_eval(2, form, ECL_NIL);
        } else {
            ret = si_safe_eval(3, form, ECL_NIL, ECL_NIL);
        }
    } ECL_CATCH_ALL_IF_CAUGHT {
    } ECL_CATCH_ALL_END;
    return ret;
}

cl_object reval(cl_object form) {
    cl_object ret = NULL;
    cl_env_ptr env = ecl_process_env();
    ECL_CATCH_ALL_BEGIN(env) {
        cl_object rev = cl_reverse(form);
        ret = eval(rev);
    } ECL_CATCH_ALL_IF_CAUGHT {
    } ECL_CATCH_ALL_END;
    return ret;
}

cl_object eval_str(const char *s) {
    return eval(ecl_read_from_cstring(s));
}

const char* repr(cl_object x) {
    return cstr(cl_prin1_to_string(x));
}

bool eval_failed(cl_object o) {
    return o == NULL;
}

bool listp(cl_object x) {
    return cl_listp(x) != ECL_NIL;
}

int length(cl_object x) {
    return fixint(cl_length(x));
}

bool ensure_list(cl_object x, int len) {
    return listp(x) && length(x) >= len;
}
%}

%import "ecl/config.h"
//%include "ecl/external.h"
//%include "ecl/stacks.h"

typedef cl_fixnum cl_narg;

extern ECL_API int cl_boot(int argc, char **argv);
extern ECL_API void cl_shutdown(void);
extern ECL_API void ecl_set_option(int option, cl_fixnum value);
extern ECL_API cl_object ecl_make_symbol(const char *s, const char*p);
extern ECL_API cl_object cl_make_array (cl_narg narg, cl_object dims, cl_object type, cl_object resize, cl_object fill, cl_object x, cl_object y);
extern ECL_API cl_object si_aset (cl_narg narg, cl_object x, cl_object y, cl_object z, cl_object a);
extern ECL_API cl_object cl_aref (cl_narg narg, cl_object a, cl_object x, cl_object y);

// Our API
extern int boot(void);
extern void set_debug(int level);
extern bool nil(cl_object o);
extern cl_object num(int x);
extern cl_object floatnum(float x);
extern cl_object str(const char *s);
extern int cint(const cl_object o);
extern float cfloat(const cl_object o);
extern const char* cstr(const cl_object o);
extern cl_object list(void);
extern cl_object list2(cl_object a, cl_object b);
extern cl_object list3(cl_object a, cl_object b, cl_object c);
extern cl_object add(cl_object l, cl_object arg);
extern cl_object call(const char *s);
extern cl_object readstr(const char *s);
extern cl_object eval(cl_object form);
extern cl_object reval(cl_object form);
extern cl_object eval_str(const char *s);
extern const char* repr(cl_object x);
extern bool eval_failed(cl_object o);
extern bool listp(cl_object x);
extern int length(cl_object x);
extern bool ensure_list(cl_object x, int len);

// cons.h
extern ECL_API cl_object ecl_car(cl_object);
extern ECL_API cl_object ecl_cdr(cl_object);
extern ECL_API cl_object ecl_caar(cl_object);
extern ECL_API cl_object ecl_cdar(cl_object);
extern ECL_API cl_object ecl_cadr(cl_object);
extern ECL_API cl_object ecl_cddr(cl_object);
extern ECL_API cl_object ecl_caaar(cl_object);
extern ECL_API cl_object ecl_cdaar(cl_object);
extern ECL_API cl_object ecl_cadar(cl_object);
extern ECL_API cl_object ecl_cddar(cl_object);
extern ECL_API cl_object ecl_caadr(cl_object);
extern ECL_API cl_object ecl_cdadr(cl_object);
extern ECL_API cl_object ecl_caddr(cl_object);
extern ECL_API cl_object ecl_cdddr(cl_object);
extern ECL_API cl_object ecl_caaaar(cl_object);
extern ECL_API cl_object ecl_cdaaar(cl_object);
extern ECL_API cl_object ecl_cadaar(cl_object);
extern ECL_API cl_object ecl_cddaar(cl_object);
extern ECL_API cl_object ecl_caadar(cl_object);
extern ECL_API cl_object ecl_cdadar(cl_object);
extern ECL_API cl_object ecl_caddar(cl_object);
extern ECL_API cl_object ecl_cdddar(cl_object);
extern ECL_API cl_object ecl_caaadr(cl_object);
extern ECL_API cl_object ecl_cdaadr(cl_object);
extern ECL_API cl_object ecl_cadadr(cl_object);
extern ECL_API cl_object ecl_cddadr(cl_object);
extern ECL_API cl_object ecl_caaddr(cl_object);
extern ECL_API cl_object ecl_cdaddr(cl_object);
extern ECL_API cl_object ecl_cadddr(cl_object);
extern ECL_API cl_object ecl_cddddr(cl_object);
